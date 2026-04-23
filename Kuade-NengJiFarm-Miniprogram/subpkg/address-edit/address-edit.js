const api = require('../../utils/api');
const QQMapWX = require('../../utils/qqmap-wx-jssdk.min.js');



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
    console.log('编辑地址页面加载', options);
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
      this.setData({
        formData: {
          name: data.name,
          phone: data.phone,
          province: data.province,
          city: data.city,
          district: data.district,
          address: data.address || (data.province + data.city + data.district),
          detail: data.address,
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

  // 选择地址（使用wx.chooseLocation打开地图选择位置）
  chooseRegion: function () {
    let that = this;

    // 使用wx.chooseLocation打开地图选择位置
    wx.chooseLocation({
      success: function(res) {
        console.log('选择位置结果：', res);
        
        // 解析逻辑封装
        const parseAddress = (fullAddress, name) => {
          let province = '', city = '', district = '', detail = name || '';
          
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
            const dMatch = rest.match(/^(.+?区|.+?县|.+?市|.+?旗)/);
            if (dMatch) {
              district = dMatch[0];
            }
          }
          
          // 如果解析失败的兜底方案
          if (!province || !city) {
            // 实在解析不出来，尝试简单的按位拆分或者留给用户手动微调
            // 但为了通过验证，我们尽量从字符串里找
            if (fullAddress.includes('省')) province = fullAddress.split('省')[0] + '省';
            if (fullAddress.includes('市')) {
              const parts = fullAddress.split('市');
              if (!province) province = parts[0] + '市';
              city = (province && parts[0].includes(province) ? parts[0].replace(province, '') : parts[0]) + '市';
            }
          }

          return { province, city, district, detail: name || fullAddress };
        };

        // 直接用正则解析（腾讯地图 Key 未配置时的兜底方案）
        const result = parseAddress(res.address, res.name);
        
        that.setData({
          'formData.province': result.province,
          'formData.city': result.city,
          'formData.district': result.district,
          'formData.address': result.province + result.city + result.district,
          'formData.detail': result.detail
        });
        
        wx.showToast({
          title: '位置获取成功',
          icon: 'success'
        });
      },
      fail: function(err) {
        console.error('选择位置失败:', err);
        if (err.errCode === 1 || err.errCode === 2) {
          wx.showToast({ title: '位置权限被拒绝，请在设置中开启', icon: 'none' });
        } else {
          wx.showToast({ title: '获取位置失败，请重试', icon: 'none' });
        }
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
    
    const phoneReg = /^1[3-9]\d{9}$/;
    if (!phoneReg.test(formData.phone)) {
      wx.showToast({ title: '请输入正确的手机号', icon: 'none' });
      return;
    }
    
    if (!formData.province || !formData.city) {
      wx.showToast({ title: '请选择完整的省市区', icon: 'none' });
      return;
    }
    
    if (!formData.detail) {
      wx.showToast({ title: '请输入详细地址', icon: 'none' });
      return;
    }
    
    wx.showLoading({ title: '保存中...' });
    
    // 准备请求数据 - 严格对应后端接口字段
    const requestData = {
      name: formData.name,
      phone: formData.phone,
      province: formData.province,
      city: formData.city,
      district: formData.district,
      address: formData.detail, // 详细地址传给后端的 address 字段
      isDefault: formData.isDefault
    };
    
    if (this.data.addressId) {
      requestData.id = this.data.addressId;
    }
    
    api.request({
      method: this.data.addressId ? 'PUT' : 'POST',
      url: '/api/user/address',
      data: requestData
    })
    .then(() => {
      wx.showToast({ title: '保存成功', icon: 'success' });
      setTimeout(() => { wx.navigateBack(); }, 1500);
    })
    .catch(err => {
      console.error('保存地址失败:', err);
      wx.showToast({ title: '保存失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  }
});