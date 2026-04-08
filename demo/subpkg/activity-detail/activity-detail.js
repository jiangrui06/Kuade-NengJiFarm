const api = require('../../utils/api');

Page({
  data: {
    activity: {},
    loading: true,
    showQRCode: false,
    qrCodeUrl: ''
  },

  onLoad: function(options) {
    const activityId = options.id;
    const paid = options.paid === 'true';
    
    if (activityId) {
      this.getActivityDetail(activityId, paid);
    }
  },

  getActivityDetail: function(activityId, paid) {
    wx.showLoading({ title: '加载中...', mask: true });

    api.request({
      url: '/api/activity/detail',
      method: 'GET',
      data: {
        id: activityId
      }
    })
      .then(data => {
        this.setData({
          activity: data || {},
          loading: false
        });
        
        // 如果支付成功，显示二维码
        if (paid) {
          this.showQRCode();
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
  showQRCode: function() {
    const activityId = this.data.activity.id;
    
    // 调用API获取二维码
    api.request({
      url: `/api/activity/${activityId}/qrcode`,
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

  contactService: function() {
    wx.showModal({
      title: '能记家庭农场客服',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  registerActivity: function() {
    const remainingSlots = this.data.activity.remainingSlots || 0;
    
    // 检查是否已报满
    if (remainingSlots <= 0) {
      wx.showToast({
        title: '已报满',
        icon: 'none'
      });
      return;
    }
    
    const price = this.data.activity.price || '¥0';
    wx.showModal({
      title: `当前剩余 ${remainingSlots} 个名额，${price}一张`,
      editable: true,
      placeholderText: '请输入票数',
      success: function(res) {
        if (res.confirm) {
          if (!res.content || res.content.trim() === '') {
            wx.showToast({
              title: '请输入购买票数',
              icon: 'none'
            });
            return;
          }
          const tickets = parseInt(res.content);
          if (tickets > 0) {
            if (tickets > remainingSlots) {
              wx.showToast({
                title: `购买数量不能超过剩余的 ${remainingSlots} 个名额`,
                icon: 'none'
              });
              return;
            }
            // 计算总价格
            const price = parseFloat(this.data.activity.price.replace('¥', ''));
            const totalPrice = price * tickets;
            
            // 跳转到支付页面
            wx.navigateTo({
              url: '/subpkg/pay/pay?totalPrice=' + totalPrice + '&activityId=' + this.data.activity.id + '&source=activity'
            });
          } else {
            wx.showToast({
              title: '请输入有效的票数',
              icon: 'none'
            });
          }
        }
      }.bind(this)
    });
  },

  previewImage: function(e) {
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

  // 返回活动详情
  backToDetail: function() {
    this.setData({ showQRCode: false });
  }
});
