Page({
  data: {
    loading: true,
    farmInfo: {
      name: '',
      mainImage: '',
      introduction: '',
      philosophy: '',
      contact: {
        address: '',
        phone: '',
        email: ''
      }
    }
  },

  onLoad: function () {
    this.loadFarmIntro();
  },

  loadFarmIntro: function () {
    const that = this;
    const api = require('../../utils/api');
    api.farm.getIntro().then(res => {
      if (res) {
        const data = res;
        const contact = data.contact || {};
        const update = {};
        if (data.name) update['farmInfo.name'] = data.name;
        if (data.introduction) update['farmInfo.introduction'] = data.introduction;
        if (data.philosophy) update['farmInfo.philosophy'] = data.philosophy;
        if (contact.address) update['farmInfo.contact.address'] = contact.address;
        if (contact.phone) update['farmInfo.contact.phone'] = contact.phone;
        if (contact.email) update['farmInfo.contact.email'] = contact.email;
        if (data.mainImage) {
          const utils = require('../../utils/utils');
          update['farmInfo.mainImage'] = utils.media.processUrl(data.mainImage);
        }
        that.setData(update);
      }
      that.setData({ loading: false });
    }).catch(() => {
      that.setData({ loading: false });
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
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});

