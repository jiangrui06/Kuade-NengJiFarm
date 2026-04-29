Page({
  data: {
    farmInfo: {
      name: '能记家庭农场',
      mainImage: '',
      introduction: '能记家庭农场致力于提供绿色、健康、有机的农产品，采用传统种植方式，不使用化学农药和化肥，确保产品的品质和安全。',
      philosophy: '我们坚持"自然、健康、可持续"的发展理念，致力于为消费者提供最优质的农产品，同时保护生态环境，实现农业的可持续发展。',
      contact: {
        address: '广东省广州市从化区',
        phone: '15876534944',
        wechat: 'njjtnc15876534944'
      }
    },
    defaultMainImage: 'http://192.168.203.56/api/file/image/farm_0000000000007.jpg'
  },

  onLoad: function () {
    this.setData({
      'farmInfo.mainImage': this.data.defaultMainImage
    });
  },

  makePhoneCall: function () {
    wx.makePhoneCall({
      phoneNumber: this.data.farmInfo.contact.phone
    });
  },

  copyAddress: function () {
    wx.setClipboardData({
      data: this.data.farmInfo.contact.address,
      success: function () {
        wx.showToast({
          title: '地址已复制',
          icon: 'success'
        });
      }
    });
  },

  openLocation: function () {
    // 这里可以使用 wx.openLocation 打开地图
    // 需要具体的经纬度
    wx.openLocation({
      latitude: 23.548, // 示例经纬度
      longitude: 113.594,
      name: this.data.farmInfo.name,
      address: this.data.farmInfo.contact.address
    });
  }
});

