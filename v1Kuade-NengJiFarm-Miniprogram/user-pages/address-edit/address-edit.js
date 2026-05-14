const api = require('../../utils/api');
const QQMapWX = require('../../utils/qqmap-wx-jssdk.js');

Page({
  data: {
    addressId: null,
    formData: {
      name: '',
      phone: '',
      province: '',
      city: '',
      district: '',
      address: '',
      detail: '',
      isDefault: false
    }
  },

  onLoad: function (options) {
    if (options.id) {
      this.setData({ addressId: options.id });
      this.loadAddressDetail(options.id);
    } else if (options.name) {
      // 从地址页面传递过来的地址信息
      this.setData({
        formData: {
          name: options.name || '',
          phone: options.phone || '',
          province: options.province || '',
          city: options.city || '',
          district: options.district || '',
          address: (options.province || '') + (options.city || '') + (options.district || ''),
          detail: options.detail || '',
          isDefault: false
        }
      });
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
      // 构建省市区地址（只包含省市区，不包含详细地址）
      const regionAddress = (data.province || '') + (data.city || '') + (data.district || '');

      this.setData({
        formData: {
          name: data.name,
          phone: data.phone,
          province: data.province,
          city: data.city,
          district: data.district,
          address: regionAddress,
          detail: data.detail || data.address || '',
          isDefault: data.isDefault
        }
      });
    })
    .catch(err => {
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

  // 获取手机号（调用微信手机号快捷登录接口）
  onGetPhoneNumber: function (e) {

    // 用户拒绝授权
    if (!e.detail.code) {
      wx.showToast({ title: '您取消了授权', icon: 'none' });
      return;
    }

    const phoneCode = e.detail.code;
    wx.showLoading({ title: '获取手机号中...' });

    // 先拿 wx.login 的 code，再一起发给后端
    wx.login({
      success: (loginRes) => {
        if (!loginRes.code) {
          wx.hideLoading();
          wx.showToast({ title: '登录凭证获取失败', icon: 'none' });
          return;
        }

        // 调用后端手机号登录接口
        api.request({
          url: '/api/Auth/wx-phone-login',
          method: 'POST',
          data: {
            code: loginRes.code,
            phoneCode: phoneCode
          },
          showLoading: false
        })
        .then((data) => {
          const phone = data.phone_number || '';

          // 更新页面上的手机号
          this.setData({
            'formData.phone': phone
          });

          // 如果后端返回了新 token，更新本地存储
          if (data.token) {
            wx.setStorageSync('token', data.token);
          }

          wx.hideLoading();
          wx.showToast({ title: '手机号获取成功', icon: 'success' });
        })
        .catch((err) => {
          wx.hideLoading();

          // 根据错误码给用户友好提示
          const errMsg = (err && err.message) || '获取手机号失败';
          if (err && err.code === 409) {
            wx.showToast({ title: '该手机号已绑定其他账号', icon: 'none' });
          } else {
            wx.showToast({ title: errMsg, icon: 'none' });
          }
        });
      },
      fail: () => {
        wx.hideLoading();
        wx.showToast({ title: '微信登录失败', icon: 'none' });
      }
    });
  },

  // 选择地址（使用wx.chooseLocation打开地图选择位置）
  chooseRegion: function () {
    let that = this;

    // 使用wx.chooseLocation打开地图选择位置
    wx.chooseLocation({
      success: function(res) {
        
        // 解析逻辑封装
        const parseAddress = (fullAddress, name) => {
          let province = '', city = '', district = '', detail = '';
          
          // 1. 提取省份/直辖市
          const pMatch = fullAddress.match(/^(.+?省|.+?自治区|北京市|天津市|上海市|重庆市)/);
          if (pMatch) {
            province = pMatch[0];
            let rest = fullAddress.substring(province.length);
            
            // 2. 提取城市 (处理直辖市情况)
            if (['北京市', '天津市', '上海市', '重庆市'].includes(province)) {
              city = province;
            } else {
              const cMatch = rest.match(/^(.+?市|.+?自治州|.+?盟|.+?地区)/);
              if (cMatch) {
                city = cMatch[0];
                rest = rest.substring(city.length);
              }
            }
            
            // 3. 提取区县
            const dMatch = rest.match(/^(.+?区|.+?县|.+?市|.+?旗|.+?特区|.+?林区)/);
            if (dMatch) {
              district = dMatch[0];
              rest = rest.substring(district.length);
            }

            // 4. 剩余部分作为详细地址，加上 POI 名称
            detail = (rest.trim() + ' ' + (name || '')).trim();
          }
          
          return { province, city, district, detail };
        };

        const parsed = parseAddress(res.address, res.name);

        // 构建省市区地址（只包含省市区，不包含详细地址）
        const regionAddress = (parsed.province || '') + (parsed.city || '') + (parsed.district || '');

        that.setData({
          'formData.province': parsed.province,
          'formData.city': parsed.city,
          'formData.district': parsed.district,
          'formData.address': regionAddress,
          'formData.detail': parsed.detail
        });
      }
    });
  },

  // 保存地址
  saveAddress: function () {
    const { formData, addressId } = this.data;

    // 表单验证
    if (!formData.name.trim()) {
      wx.showToast({ title: '请输入收货人姓名', icon: 'none' });
      return;
    }
    if (!formData.phone.trim() || !/^1[3-9]\d{9}$/.test(formData.phone)) {
      wx.showToast({ title: '请输入有效的手机号', icon: 'none' });
      return;
    }
    if (!formData.address.trim()) {
      wx.showToast({ title: '请选择或输入所在地区', icon: 'none' });
      return;
    }
    if (!formData.detail.trim()) {
      wx.showToast({ title: '请输入详细地址', icon: 'none' });
      return;
    }

    wx.showLoading({ title: '保存中...' });

    const requestUrl = addressId ? `/api/address/${addressId}` : '/api/address';
    const method = addressId ? 'PUT' : 'POST';

    api.request({
      url: requestUrl,
      method: method,
      data: {
        ...formData,
        id: addressId
      }
    })
    .then(() => {
      wx.showToast({ title: '保存成功', icon: 'success' });
      setTimeout(() => {
        wx.navigateBack();
      }, 1500);
    })
    .catch(err => {
      wx.showToast({ title: '保存失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  }
});

