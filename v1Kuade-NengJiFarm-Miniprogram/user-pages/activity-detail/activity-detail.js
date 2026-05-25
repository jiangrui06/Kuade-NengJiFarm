const api = require('../../utils/api');

Page({
  data: {
    activity: {},
    loading: true,
    showQRCode: false,
    qrCodeUrl: '',
    orderId: '',
    hasVideo: false
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
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  // 获取活动图片列表：兼容多种字段名
  _getActivityImages: function (data) {
    let rawImages = data.specImages || data.detailImages || data.images || data.carouselMedia || data.imageList || data.bannerList || [];
    if (!Array.isArray(rawImages)) return [];
    return rawImages.map(item => {
      if (typeof item === 'string') return this.processImageUrl(item);
      if (item && typeof item === 'object') return this.processImageUrl(item.image || item.url || item.src || '');
      return '';
    }).filter(Boolean);
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
        // 处理活动图片路径和日期格式
        let dateStr = data.date || '';
        if (dateStr && !/\d{4}/.test(dateStr)) {
          const year = new Date().getFullYear();
        }

        const processedActivity = {
          ...data,
          image: this.processImageUrl(data.image),
          images: this._getActivityImages(data),
          price: typeof data.price === 'string' ? data.price.replace(/[¥￥]/g, '') : data.price,
          date: dateStr
        };

        // 视频处理
        let videoUrl = '';
        const rawVideoUrl = data.videoUrl || data.video || data.video_url || '';
        if (rawVideoUrl) {
          videoUrl = String(rawVideoUrl).startsWith('http') ? String(rawVideoUrl) : this.processImageUrl(String(rawVideoUrl));
        }
        
        this.setData({
          activity: processedActivity || {},
          loading: false,
          orderId: orderId || this.data.orderId,
          hasVideo: !!videoUrl
        });
        
        // 有视频时写入 activity 对象供 WXML 使用
        if (videoUrl) {
          this.setData({
            'activity.videoUrl': videoUrl
          });
        }

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

    api.order.getQrcode(targetOrderId)
      .then(data => {
        this.setData({
          qrCodeUrl: data.qrCodeUrl || '',
          showQRCode: true
        });
      })
      .catch(err => {
        wx.showToast({
          title: '获取二维码失败',
          icon: 'none'
        });
      });
  },

  registerActivity: function () {
    const remainingSlots = this.data.activity.remainingSlots || 0;
    if (remainingSlots <= 0) {
      wx.showToast({
        title: '已售罄',
        icon: 'none'
      });
      return;
    }

    this.showRegisterModal(remainingSlots);
  },

  // 显示报名弹窗
  showRegisterModal: function (remainingSlots) {
    const priceText = this.data.activity.price || '0';
    const that = this;
    wx.showModal({
      title: `当前剩余 ${remainingSlots} 个名额，${priceText} 元/人`,
      editable: true,
      placeholderText: '请输入购票张数',
      success: function (res) {
        if (!res.confirm) {
          return;
        }

        if (!res.content || res.content.trim() === '') {
          wx.showToast({
            title: '请输入购票张数',
            icon: 'none'
          });
          return;
        }

        const inputContent = res.content.trim();
        // 检查是否包含小数点或其他非数字字符
        if (!/^\d+$/.test(inputContent)) {
          wx.showToast({
            title: '请输入整数张数',
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
            title: `购票数量不能超过剩余 ${remainingSlots} 个名额`,
            icon: 'none'
          });
          return;
        }

        wx.showLoading({ title: '下单中...', mask: true })

        api.request({
          url: `/api/activity/${that.data.activity.id}/register`,
          method: 'POST',
          data: {
            tickets: tickets
          },
          showLoading: false
        })
          .then(orderData => {
            wx.hideLoading();
            const orderNo = orderData.orderNo || orderData.orderId || orderData.id;
            if (!orderNo) {
              wx.showToast({ title: '下单失败', icon: 'none' });
              return;
            }
            // 跳转支付
            wx.redirectTo({
              url: `/user-pages/pay/pay?orderNo=${orderNo}&type=activity&activityId=${that.data.activity.id}`
            });
          })
          .catch(err => {
            wx.hideLoading();
            wx.showToast({
              title: err.message || '下单失败',
              icon: 'none'
            });
          });
      }
    });
  },

  hideQRCode: function () {
    this.setData({
      showQRCode: false
    });
  },

  // 预览图片
  previewImage: function (e) {
    const current = e.currentTarget.dataset.src;
    const urls = this.data.activity.images || [this.data.activity.image];
    wx.previewImage({
      current: current,
      urls: urls
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    if (this.data.activity && this.data.activity.id) {
      this.getActivityDetail(this.data.activity.id, false, this.data.orderId);
    }
    // 刷新完成后停止下拉刷新
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  }
});

