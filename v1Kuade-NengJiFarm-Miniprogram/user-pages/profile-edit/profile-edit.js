const api = require('../../utils/api');

Page({
  data: {
    userInfo: {
      nickname: '',
      avatar: '',
      gender: '',
      phone: ''
    }
  },

  onLoad: function () {
    console.log('基本信息页面加载');
    this.getUserProfile();
  },

  // 获取用户信息
  getUserProfile: function () {
    wx.showLoading({ title: '加载中...' });
    
    api.user.getProfile()
    .then(data => {
      this.setData({
        userInfo: {
          nickname: data.nickname || '',
          avatar: this.processImageUrl(data.avatar || ''),
          gender: data.gender || '',
          phone: data.phone || ''
        }
      });
    })
    .catch(err => {
      console.error('获取用户信息失败:', err);
      wx.showToast({ title: '加载失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  },

  // 检测是否为临时路径
  isTempPath: function (path) {
    return path && (path.startsWith('http://tmp/') || path.startsWith('https://tmp/'));
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 检测是否为临时路径
    if (this.isTempPath(imageUrl)) {
      return imageUrl;
    }
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      return imageUrl.replace('http://192.168.203.56', 'http://192.168.203.56');
    }
    
    // 如果是相对路径，添加基础 URL
    const baseUrl = 'http://192.168.203.56';
    if (!imageUrl.startsWith('/')) {
      imageUrl = '/' + imageUrl;
    }
    return baseUrl + imageUrl;
  },

  // 选择头像（使用微信官方组件）
  onChooseAvatar: function (e) {
    const avatarUrl = e.detail.avatarUrl;
    console.log('选择的头像临时路径', avatarUrl);
    
    // 获取 token
    const token = wx.getStorageSync('token');
    
    // 上传图片到服务器
    wx.uploadFile({
      url: 'http://192.168.203.56/api/file/upload/avatar',
      filePath: avatarUrl,
      name: 'file',
      header: {
        Authorization: 'Bearer ' + token
      },
      success: (res) => {
        console.log('上传成功响应:', res);
        try {
          if (!res.data || res.data.trim() === '') {
            console.error('服务器返回空响应');
            wx.showToast({ title: '上传失败', icon: 'none' });
            return;
          }
          
          const data = JSON.parse(res.data);
          console.log('解析后的响应数据:', data);
          if (data.code === 0) {
            this.setData({
              'userInfo.avatar': data.data.url
            });
            wx.showToast({ title: '上传成功', icon: 'success' });
          } else {
            console.error('上传头像失败:', data.message);
            wx.showToast({ title: data.message || '上传失败', icon: 'none' });
          }
        } catch (e) {
          console.error('解析上传结果失败:', e);
          wx.showToast({ title: '上传失败', icon: 'none' });
        }
      },
      fail: (err) => {
        console.error('上传头像失败:', err);
        wx.showToast({ title: '上传失败', icon: 'none' });
      }
    });
  },

  // 选择性别
  chooseGender: function () {
    wx.showActionSheet({
      itemList: ['男', '女'],
      success: (res) => {
        const genders = ['男', '女'];
        this.setData({
          'userInfo.gender': genders[res.tapIndex]
        });
      }
    });
  },

  // 昵称变化
  onNicknameChange: function (e) {
    this.setData({
      'userInfo.nickname': e.detail.value
    });
  },

  // 获取手机号
  onGetPhoneNumber: function (e) {
    console.log('获取手机号回调', e);

    if (!e.detail.code) {
      wx.showToast({ title: '您取消了授权', icon: 'none' });
      return;
    }

    const phoneCode = e.detail.code;
    wx.showLoading({ title: '获取手机号中...' });

    wx.login({
      success: (loginRes) => {
        if (!loginRes.code) {
          wx.hideLoading();
          wx.showToast({ title: '登录凭证获取失败', icon: 'none' });
          return;
        }

        api.auth.phoneLogin({
          code: loginRes.code,
          phoneCode: phoneCode
        })
        .then((data) => {
          console.log('手机号获取成功', data);
          const phone = data.phone_number || '';
          this.setData({
            'userInfo.phone': phone
          });
          wx.hideLoading();
          wx.showToast({ title: '手机号获取成功', icon: 'success' });
        })
        .catch((err) => {
          console.error('获取手机号失败', err);
          wx.hideLoading();
          wx.showToast({ title: err.message || '获取失败', icon: 'none' });
        });
      },
      fail: () => {
        wx.hideLoading();
        wx.showToast({ title: '微信登录失败', icon: 'none' });
      }
    });
  },

  // 保存个人资料
  saveProfile: function () {
    const { userInfo } = this.data;
    if (!userInfo.nickname.trim()) {
      wx.showToast({ title: '请输入昵称', icon: 'none' });
      return;
    }

    wx.showLoading({ title: '保存中...' });
    api.user.updateProfile(userInfo)
    .then(() => {
      wx.showToast({ title: '保存成功', icon: 'success' });
      // 触发上一个页面的刷新
      const pages = getCurrentPages();
      const prevPage = pages[pages.length - 2];
      if (prevPage && prevPage.getUserProfilePreview) {
        prevPage.getUserProfilePreview();
      }
      setTimeout(() => {
        wx.navigateBack();
      }, 1500);
    })
    .catch(err => {
      console.error('保存个人资料失败:', err);
      wx.showToast({ title: '保存失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  }
});

