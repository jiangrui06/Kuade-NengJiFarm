const api = require('../../utils/api');
const QQMapWX = require('../../utils/qqmap-wx-jssdk.min');

// 腾讯地图实例（Key 填你自己的，在 https://lbs.qq.com 申请）
const qqmapsdk = new QQMapWX({
  key: 'AEZBZ-OUVCJ-VJJFF-D5ORS-LY7W7-QVB6K'
});

Page({
  data: {
    addressList: [],
    locating: false,       // 正在定位中
    currentLocation: null  // 当前定位结果
  },

  onLoad: function () {
    this.getAddressList();
  },

  // ─────────────────────────────────────────
  // 获取当前位置（微信官方 API + 腾讯逆地址解析）
  // ─────────────────────────────────────────
  getLocation: function () {
    if (this.data.locating) return;
    this.setData({ locating: true });

    wx.showLoading({ title: '定位中...', mask: true });

    // Step 1：申请授权
    wx.getSetting({
      success: (res) => {
        if (res.authSetting['scope.userLocation'] === false) {
          // 用户曾拒绝过，引导去设置页重新开启
          wx.hideLoading();
          this.setData({ locating: false });
          wx.showModal({
            title: '需要位置权限',
            content: '请在设置中开启位置权限，以便获取当前地址',
            confirmText: '去设置',
            success: (r) => {
              if (r.confirm) wx.openSetting();
            }
          });
          return;
        }
        // Step 2：获取经纬度
        this._doGetLocation();
      },
      fail: () => {
        wx.hideLoading();
        this.setData({ locating: false });
        wx.showToast({ title: '获取权限失败', icon: 'none' });
      }
    });
  },

  _doGetLocation: function () {
    wx.getLocation({
      type: 'gcj02',   // 腾讯地图使用 gcj02 坐标系
      success: (res) => {
        const { latitude, longitude } = res;
        // Step 3：逆地址解析（经纬度 → 省市区街道）
        qqmapsdk.reverseGeocoder({
          location: { latitude, longitude },
          success: (geo) => {
            wx.hideLoading();
            const result = geo.result;
            const ad = result.address_component;
            const location = {
              latitude,
              longitude,
              fullAddress: result.address,          // 完整地址
              province:    ad.province,
              city:        ad.city,
              district:    ad.district,
              street:      ad.street,
              streetNumber: ad.street_number
            };
            this.setData({ locating: false, currentLocation: location });
            this._confirmUseLocation(location);
          },
          fail: (err) => {
            wx.hideLoading();
            this.setData({ locating: false });
            console.error('逆地址解析失败:', err);
            wx.showToast({ title: '地址解析失败', icon: 'none' });
          }
        });
      },
      fail: (err) => {
        wx.hideLoading();
        this.setData({ locating: false });
        console.error('getLocation 失败:', err);
        wx.showToast({ title: '定位失败，请检查权限', icon: 'none' });
      }
    });
  },

  // 弹窗确认是否使用当前位置
  _confirmUseLocation: function (location) {
    wx.showModal({
      title: '当前位置',
      content: location.fullAddress,
      confirmText: '使用此地址',
      cancelText: '取消',
      success: (res) => {
        if (res.confirm) {
          // 跳转到新增地址页，并预填当前位置
          wx.navigateTo({
            url: `/subpkg/address-edit/address-edit?province=${encodeURIComponent(location.province)}&city=${encodeURIComponent(location.city)}&district=${encodeURIComponent(location.district)}&street=${encodeURIComponent(location.street + location.streetNumber)}&lat=${location.latitude}&lng=${location.longitude}`
          });
        }
      }
    });
  },

  // ─────────────────────────────────────────
  // 以下为原有逻辑，保持不变
  // ─────────────────────────────────────────
  getAddressList: function () {
    wx.showLoading({ title: '加载中...' });
    api.request({
      url: '/api/address/list',
      method: 'GET'
    })
    .then(data => {
      this.setData({ addressList: data || [] });
    })
    .catch(err => {
      console.error('获取地址列表失败:', err);
      wx.showToast({ title: '加载失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  },

  selectAddress: function (e) {
    const addressId = e.currentTarget.dataset.id;
    const selectedAddress = this.data.addressList.find(item => item.id === addressId);
    const pages = getCurrentPages();
    const prevPage = pages[pages.length - 2];
    if (prevPage) {
      prevPage.setData({ selectedAddress });
      wx.navigateBack();
    }
  },

  addAddress: function () {
    wx.navigateTo({ url: '/subpkg/address-edit/address-edit' });
  },

  editAddress: function (e) {
    const addressId = e.currentTarget.dataset.id;
    wx.navigateTo({ url: `/subpkg/address-edit/address-edit?id=${addressId}` });
  },

  deleteAddress: function (e) {
    const addressId = e.currentTarget.dataset.id;
    wx.showModal({
      title: '确认删除',
      content: '确定要删除这个收货地址吗？',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '删除中...' });
          api.request({
            url: `/api/address/${addressId}`,
            method: 'DELETE'
          })
          .then(() => {
            wx.showToast({ title: '删除成功', icon: 'success' });
            this.getAddressList();
          })
          .catch(err => {
            console.error('删除地址失败:', err);
            wx.showToast({ title: '删除失败', icon: 'none' });
          })
          .finally(() => {
            wx.hideLoading();
          });
        }
      }
    });
  },

  goBack: function () {
    wx.navigateBack();
  }
});
