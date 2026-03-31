const api = require('../../utils/api');
const QQMapWX = require('../../utils/qqmap-wx-jssdk.min.js');
const qqmapsdk = new QQMapWX({
  key: '6R6BZ-7SHJW-WDPKT-LTC4N-6UXD6-48RVW'
});

Page({
  data: {
    addressId: null,
    formData: {
      name: '',
      phone: '',
      province: '',
      city: '',
      district: '',
      detail: '',
      isDefault: false
    }
  },

  onLoad: function (options) {
    console.log('编辑地址页面加载', options);
    if (options.id) {
      this.setData({ addressId: options.id });
      this.loadAddressDetail(options.id);
    }
  },

  // 加载地址详情
  loadAddressDetail: function (id) {
    wx.showLoading({ title: '加载中...' });
    
    api.request({
      url: `/api/address/${id}`,
      method: 'GET'
    })
    .then(data => {
      this.setData({
        formData: {
          name: data.name,
          phone: data.phone,
          province: data.province,
          city: data.city,
          district: data.district,
          detail: data.detail,
          isDefault: data.isDefault
        }
      });
    })
    .catch(err => {
      console.error('获取地址详情失败:', err);
      wx.showToast({ title: '加载失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  },

  // 输入框输入事件
  onInputChange: function (e) {
    const { field } = e.currentTarget.dataset;
    const { value } = e.detail;
    
    this.setData({
      [`formData.${field}`]: value
    });
  },

  // 切换默认地址
  toggleDefault: function () {
    this.setData({
      'formData.isDefault': !this.data.formData.isDefault
    });
  },

  // 选择地址
  chooseRegion: function () {
    let that = this;
    // 1. 先判断用户是否授权位置权限
    wx.getSetting({
      success(res) {
        // 未授权 -> 引导授权
        if (!res.authSetting['scope.userLocation']) {
          wx.authorize({
            scope: 'scope.userLocation',
            success() {
              // 授权成功 -> 获取位置
              that.getLocationInfo();
            },
            fail() {
              // 授权失败 -> 提示去设置页开启
              wx.showModal({
                title: '提示',
                content: '请开启位置权限',
                confirmText: '去设置',
                success(res) {
                  if (res.confirm) {
                    wx.openSetting();
                  }
                }
              })
            }
          })
        } else {
          // 已授权 -> 直接获取位置
          that.getLocationInfo();
        }
      }
    })
  },

  // 获取经纬度 + 逆地址解析
  getLocationInfo: function () {
    let that = this;
    wx.showLoading({ title: '定位中...' });

    // 调用微信API获取经纬度
    wx.getLocation({
      type: 'gcj02', // 国测局坐标（腾讯地图支持）
      success(res) {
        console.log('经纬度：', res);

        // 腾讯地图逆地址解析（经纬度转文字地址）
        qqmapsdk.reverseGeocoder({
          location: {
            latitude: res.latitude,
            longitude: res.longitude
          },
          success(res) {
            console.log('详细地址：', res);
            // 获取完整地址信息
            const addressResult = res.result;
            that.setData({
              'formData.province': addressResult.address_component.province,
              'formData.city': addressResult.address_component.city,
              'formData.district': addressResult.address_component.district,
              'formData.detail': that.data.formData.detail || addressResult.address
            });
          },
          fail(err) {
            console.error('地址解析失败:', err);
            wx.showToast({ title: '地址解析失败', icon: 'error' });
          },
          complete() {
            wx.hideLoading();
          }
        });
      },
      fail(err) {
        console.error('定位失败:', err);
        wx.hideLoading();
        wx.showToast({ title: '定位失败', icon: 'error' });
      }
    });
  },

  // 保存地址
  saveAddress: function () {
    const { formData } = this.data;
    
    // 表单验证
    if (!formData.name) {
      wx.showToast({ title: '请输入收件人姓名', icon: 'none' });
      return;
    }
    
    if (!formData.phone) {
      wx.showToast({ title: '请输入手机号', icon: 'none' });
      return;
    }
    
    // 手机号验证
    const phoneReg = /^1[3-9]\d{9}$/;
    if (!phoneReg.test(formData.phone)) {
      wx.showToast({ title: '请输入正确的手机号', icon: 'none' });
      return;
    }
    
    if (!formData.province || !formData.city || !formData.district) {
      wx.showToast({ title: '请选择省市区', icon: 'none' });
      return;
    }
    
    if (!formData.detail) {
      wx.showToast({ title: '请输入详细地址', icon: 'none' });
      return;
    }
    
    wx.showLoading({ title: '保存中...' });
    
    const requestConfig = {
      method: this.data.addressId ? 'PUT' : 'POST',
      url: this.data.addressId ? `/api/address/${this.data.addressId}` : '/api/address',
      data: formData
    };
    
    api.request(requestConfig)
    .then(() => {
      wx.showToast({ title: '保存成功', icon: 'success' });
      // 返回上一页
      wx.navigateBack();
    })
    .catch(err => {
      console.error('保存地址失败:', err);
      wx.showToast({ title: '保存失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  },

  // 返回上一页
  goBack: function () {
    wx.navigateBack();
  }
});