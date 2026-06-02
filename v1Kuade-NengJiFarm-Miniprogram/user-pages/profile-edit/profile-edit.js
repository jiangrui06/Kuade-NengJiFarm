const api = require('../../utils/api');

Page({
  data: {
    userInfo: {
      nickname: '',
      avatar: '',
      gender: '',
      phone: '',
      role: 'user'
    },
    // 角色显示文案映射
    roleTextMap: {
      'user': '普通用户',
      'staff': '员工',
      'admin': '管理员'
    }
  },

  onLoad: function () {
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
          phone: data.phone || '',
          role: data.role || 'user'
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

  // 检测是否为临时路径
  isTempPath: function (path) {
    return path && (path.startsWith('http://tmp/') || path.startsWith('https://tmp/'));
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    // 检测是否为临时路径
    if (this.isTempPath(imageUrl)) {
      return imageUrl;
    }
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  // 选择头像（使用微信官方组件）
  onChooseAvatar: function (e) {
    const avatarUrl = e.detail.avatarUrl;
    
    // 获取 token
    const token = wx.getStorageSync('token');
    
    // 上传图片到服务器
    wx.uploadFile({
      url: (getApp().globalData.baseUrl || 'https://api.nengjifarm.com') + '/api/file/upload/avatar',
      filePath: avatarUrl,
      name: 'file',
      header: {
        Authorization: 'Bearer ' + token
      },
      success: (res) => {
        try {
          if (!res.data || res.data.trim() === '') {
            wx.showToast({ title: '上传失败', icon: 'none' });
            return;
          }
          
          const data = JSON.parse(res.data);
          if (data.code === 0) {
            this.setData({
              'userInfo.avatar': data.data.url
            });
            wx.showToast({ title: '上传成功', icon: 'success' });
          } else {
            wx.showToast({ title: data.message || '上传失败', icon: 'none' });
          }
        } catch (e) {
          wx.showToast({ title: '上传失败', icon: 'none' });
        }
      },
      fail: (err) => {
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

  // 选择角色
  chooseRole: function () {
    const roleOptions = ['普通用户(user)', '员工(staff)'];
    wx.showActionSheet({
      itemList: roleOptions,
      success: (res) => {
        const roles = ['user', 'staff'];
        this.setData({
          'userInfo.role': roles[res.tapIndex]
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
      wx.showToast({ title: '保存失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  }
});

