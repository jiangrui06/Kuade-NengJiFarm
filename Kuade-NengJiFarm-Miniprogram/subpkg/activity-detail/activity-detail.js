const api = require('../../utils/api');

Page({
  data: {
    activity: {},
    loading: true,
    showQRCode: false,
    qrCodeUrl: '',
    orderId: ''
  },

  onLoad: function (options) {
    const activityId = options.id;
    const paid = options.paid === 'true';
    const orderId = options.orderId || '';

    this.activityId = activityId;
    this.paid = paid;
    this.orderId = orderId;

    if (activityId) {
      this.getActivityDetail(activityId, paid, orderId);
    }
  },

  onShow: function () {
    // 当页面显示时，只有在从支付页面返回时才重新加载活动数据
    // 避免每次页面显示都重新加载，提升用户体验
    if (this.activityId && this.paid) {
      this.getActivityDetail(this.activityId, this.paid, this.orderId);
      // 重置paid状态，避免下次页面显示时重复加载
      this.paid = false;
    }
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      // 替换 192.168.203.56 为 192.168.203.56
      return imageUrl.replace('http://192.168.101.47', 'http://192.168.101.47');
    }
    
    // 如果是相对路径，添加基础 URL
    return 'http://192.168.101.47' + imageUrl;
  },

  getActivityDetail: function (activityId, paid, orderId = '') {
    wx.showLoading({ title: '加载中...', mask: true });

    api.request({
      url: '/api/activity/detail',
      method: 'GET',
      data: {
        id: activityId
      },
      showLoading: false
    })
      .then(data => {
        // 处理活动图片路径
        const processedActivity = {
          ...data,
          image: this.processImageUrl(data.image),
          images: (data.images || []).map(image => this.processImageUrl(image)),
          price: typeof data.price === 'string' ? data.price.replace(/[¥￥]/g, '') : data.price // 清理价格符号
        };
        
        this.setData({
          activity: processedActivity || {},
          loading: false,
          orderId: orderId || this.data.orderId
        });

        // 如果支付成功，显示二维码
        if (paid) {
          this.showQRCode(orderId);
        }
      })
      .catch(err => {
        wx.showToast({
          title: err.message || '活动详情加载失败',
          icon: 'none'
        });
        this.setData({ loading: false });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  // 显示二维码
  showQRCode: function (orderId = '') {
    const targetOrderId = orderId || this.data.orderId;
    if (!targetOrderId) {
      wx.showToast({
        title: '缺少订单ID',
        icon: 'none'
      });
      return;
    }

    api.request({
      url: `/api/orders/${targetOrderId}/qrcode`,
      method: 'GET'
    })
      .then(data => {
        this.setData({
          qrCodeUrl: data.qrCodeUrl || '',
          showQRCode: true
        });
      })
      .catch(err => {
        console.error('获取二维码失败:', err);
        wx.showToast({
          title: '获取二维码失败',
          icon: 'none'
        });
      });
  },

  contactService: function () {
    wx.showModal({
      title: '能记家庭农场客服',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  registerActivity: function () {
    const remainingSlots = this.data.activity.remainingSlots || 0;
    if (remainingSlots <= 0) {
      wx.showToast({
        title: '已报满',
        icon: 'none'
      });
      return;
    }

    const priceText = this.data.activity.price || '￥0';
    wx.showModal({
      title: `当前剩余 ${remainingSlots} 个名额，${priceText}一张`,
      editable: true,
      placeholderText: '请输入票数',
      success: function (res) {
        if (!res.confirm) {
          return;
        }

        if (!res.content || res.content.trim() === '') {
          wx.showToast({
            title: '请输入购买票数',
            icon: 'none'
          });
          return;
        }

        const inputContent = res.content.trim();
        // 检查是否包含小数点或其他非数字字符
        if (!/^\d+$/.test(inputContent)) {
          wx.showToast({
            title: '请输入整数票数',
            icon: 'none'
          });
          return;
        }

        const tickets = parseInt(inputContent, 10);
        if (!(tickets > 0)) {
          wx.showToast({
            title: '请输入有效的票数',
            icon: 'none'
          });
          return;
        }

        if (tickets > remainingSlots) {
          wx.showToast({
            title: `购买数量不能超过剩余的 ${remainingSlots} 个名额`,
            icon: 'none'
          });
          return;
        }

        const unitPrice = parseFloat(String(this.data.activity.price || 0).replace(/[^0-9.]/g, '')) || 0;
        const totalPrice = Number((unitPrice * tickets).toFixed(2));
        if (totalPrice <= 0) {
          wx.showToast({
            title: '活动价格异常',
            icon: 'none'
          });
          return;
        }

        wx.showLoading({ title: '下单中...' });
        api.request({
          url: `/api/activity/${this.data.activity.id}/register`,
          method: 'POST',
          data: {
            sourceType: 'activity',
            sourceName: this.data.activity.title || '活动',
            quantity: tickets,
            totalPrice,
            items: [
              {
                id: String(this.data.activity.id || ''),
                name: this.data.activity.title || '活动',
                price: Number(unitPrice.toFixed(2)),
                quantity: tickets,
                image: this.data.activity.image || ''
              }
            ]
          },
          showLoading: false
        })
          .then((orderData) => {
            const orderId = orderData.orderId || orderData.id;
            if (!orderId) {
              wx.showToast({
                title: '创建订单失败',
                icon: 'none'
              });
              return;
            }

            // 更新活动剩余名额
            const updatedActivity = {
              ...this.data.activity,
              remainingSlots: Math.max(0, (this.data.activity.remainingSlots || 0) - tickets)
            };
            this.setData({
              activity: updatedActivity
            });

            wx.navigateTo({
              url: `/subpkg/pay/pay?orderId=${orderId}&totalPrice=${totalPrice.toFixed(2)}&activityId=${this.data.activity.id}&source=activity`
            });
          })
          .catch((err) => {
            console.error('创建活动订单失败:', err);
            wx.showToast({
              title: '创建订单失败',
              icon: 'none'
            });
          })
          .finally(() => {
            wx.hideLoading();
          });
      }.bind(this)
    });
  },

  // 返回活动列表
  goBack: function() {
    wx.switchTab({
      url: '/pages/activity/activity'
    });
  },

  previewImage: function (e) {
    const index = e.currentTarget.dataset.index;
    const images = this.data.activity.images || [];
    if (images.length === 0) {
      return;
    }

    wx.previewImage({
      current: images[index],
      urls: images
    });
  },

  // 返回活动页面
  backToDetail: function () {
    wx.switchTab({
      url: '/pages/activity/activity'
    });
  },

  // 自定义返回按钮逻辑，跳转到活动页面
  onBackPress: function () {
    wx.redirectTo({
      url: '/subpkg/activity/activity'
    });
    return true; // 阻止默认返回行为
  },

  // 下拉刷新
  onPullDownRefresh: function () {
    console.log('下拉刷新活动详情');
    if (this.activityId) {
      this.getActivityDetail(this.activityId, false, this.orderId);
    }
    // 刷新完成后停止下拉刷新
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  }
});
