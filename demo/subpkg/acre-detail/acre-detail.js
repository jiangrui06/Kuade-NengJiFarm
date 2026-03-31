Page({
  data: {
    acreDetail: {},
    swiperList: []
  },
  onLoad: function(options) {
    const id = options.id;
    this.loadAcreDetail(id);
  },
  
  loadAcreDetail: function(id) {
    const api = require('../../utils/api');
    
    wx.showLoading({
      title: '加载中...',
    });
    
    api.request({
      url: '/api/acres/' + id,
      method: 'GET'
    })
    .then(data => {
      wx.hideLoading();
      
      // 清理图片路径中的反引号和空格
      const cleanData = {
        ...data.acreDetail,
        image: data.acreDetail.image ? data.acreDetail.image.replace(/[`\s]/g, '') : '',
        longExampleImage: data.acreDetail.longExampleImage ? data.acreDetail.longExampleImage.replace(/[`\s]/g, '') : '',
        swiperList: (data.acreDetail.swiperList || []).map(item => ({
          ...item,
          image: item.image ? item.image.replace(/[`\s]/g, '') : ''
        })),
        longExampleImages: (data.acreDetail.longExampleImages || []).map(image => image.replace(/[`\s]/g, '')),
        longExampleImageList: (data.acreDetail.longExampleImageList || []).map(image => image.replace(/[`\s]/g, '')),
        bottomImages: (data.acreDetail.bottomImages || []).map(image => image.replace(/[`\s]/g, ''))
      };
      
      // 确保数据结构完整
      const acreDetail = {
        ...cleanData,
        videoUrl: cleanData.videoUrl , // 视频URL
        remainingAcres: cleanData.remainingAcres , // 剩余亩数
        soldAcres: cleanData.soldAcres , // 已售亩数
        longExampleImage: cleanData.longExampleImage  // 农场示例图片
      };
      
      this.setData({
        acreDetail: acreDetail,
        swiperList: cleanData.swiperList // 轮播图数据
      });
    })
    .catch(err => {
      wx.hideLoading();
      wx.showToast({
        title: '加载失败，请重试',
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
  
  confirmPurchase: function() {
    const remainingAcres = this.data.acreDetail.remainingAcres || 0;
    const price = this.data.acreDetail.price || '¥0';
    
    // 检查是否已卖完
    if (remainingAcres <= 0) {
      wx.showToast({
        title: '已卖完',
        icon: 'none'
      });
      return;
    }
    
    wx.showModal({
      title: `当前剩余 ${remainingAcres} 亩，${price}`,
      editable: true,
      placeholderText: '请输入亩数',
      success: function(res) {
        if (res.confirm) {
          if (!res.content || res.content.trim() === '') {
            wx.showToast({
              title: '请输入购买亩数',
              icon: 'none'
            });
            return;
          }
          const acres = parseInt(res.content);
          if (acres > 0) {
            if (acres > remainingAcres) {
              wx.showToast({
                title: `购买数量不能超过剩余的 ${remainingAcres} 亩`,
                icon: 'none'
              });
              return;
            }
            // 计算总价格
            const price = parseFloat(this.data.acreDetail.price.replace(/[^0-9.]/g, ''));
            const totalPrice = price * acres;
            
            // 跳转到支付页面
            wx.navigateTo({
              url: '/subpkg/pay/pay?totalPrice=' + totalPrice
            });
          } else {
            wx.showToast({
              title: '请输入有效的亩数',
              icon: 'none'
            });
          }
        }
      }.bind(this)
    });
  }
});