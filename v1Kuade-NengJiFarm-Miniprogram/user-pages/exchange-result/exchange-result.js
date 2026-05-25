const { api } = require('../../utils/api');

Page({
  data: {
    loading: true,
    exchangeInfo: null,
    goodsName: '',
    goodsImage: '',
    pointsSpent: 0,
    pointsRemaining: 0,
    orderNo: '',
    qrcodeUrl: '',
    status: '',
    statusText: '',
    exchangeTime: '',
    verifyTime: '',
    expired: false
  },

  onLoad(options) {
    const orderNo = options.orderNo || '';
    if (!orderNo) {
      wx.showToast({ title: '缺少订单号', icon: 'none' });
      return;
    }
    this.setData({ orderNo });
    this.loadExchangeDetail(orderNo);
  },

  loadExchangeDetail(orderNo) {
    this.setData({ loading: true });

    api.points.exchangeDetail(orderNo)
      .then(data => {
        if (!data) {
          this.setData({ loading: false });
          wx.showToast({ title: '未找到兑换记录', icon: 'none' });
          return;
        }

        this.setData({
          loading: false,
          exchangeInfo: data,
          goodsName: data.name || '',
          goodsImage: this._processImage(data.image),
          pointsSpent: data.pointsSpent || 0,
          pointsRemaining: data.pointsRemaining || 0,
          orderNo: data.orderNo || orderNo,
          qrcodeUrl: this._processImage(data.qrcodeUrl) || '',
          status: ({
            '待核销': 'pending',
            '已核销': 'verified',
            '已取消': 'cancelled',
            '已完成': 'completed'
          })[data.status] || data.status || '',
          statusText: data.statusText || '待核销',
          exchangeTime: data.time || '',
          verifyTime: data.verifyTime || '',
          expired: data.expired || false
        });
      })
      .catch(() => {
        this.setData({ loading: false });
        wx.showToast({ title: '加载失败', icon: 'none' });
      });
  },

  _processImage(image) {
    if (!image) return '';
    if (image.startsWith('data:')) return image;
    if (image.startsWith('http')) return image;
    const baseUrl = 'http://192.168.101.75';
    if (image.startsWith('/api/')) return baseUrl + image;
    return baseUrl + '/api/file/image/' + image;
  },

  saveQrcode() {
    const { qrcodeUrl } = this.data;
    if (!qrcodeUrl) {
      wx.showToast({ title: '暂无二维码', icon: 'none' });
      return;
    }

    wx.showLoading({ title: '保存中...' });

    // Base64 data URL → 写入临时文件 → 保存到相册
    if (qrcodeUrl.startsWith('data:')) {
      const fs = wx.getFileSystemManager();
      const filePath = `${wx.env.USER_DATA_PATH}/qrcode_${Date.now()}.png`;
      fs.writeFile({
        filePath,
        data: qrcodeUrl.replace(/^data:image\/\w+;base64,/, ''),
        encoding: 'base64',
        success() {
          wx.saveImageToPhotosAlbum({
            filePath,
            success() {
              wx.hideLoading();
              wx.showToast({ title: '已保存到相册', icon: 'success' });
            },
            fail(err) {
              wx.hideLoading();
              if (err.errMsg && err.errMsg.includes('auth deny')) {
                wx.showModal({
                  title: '需要授权',
                  content: '请允许保存图片到相册',
                  success: (modalRes) => {
                    if (modalRes.confirm) wx.openSetting();
                  }
                });
              } else {
                wx.showToast({ title: '保存失败', icon: 'none' });
              }
            }
          });
        },
        fail() {
          wx.hideLoading();
          wx.showToast({ title: '保存失败', icon: 'none' });
        }
      });
      return;
    }

    // 普通 URL → downloadFile
    wx.downloadFile({
      url: qrcodeUrl,
      success: (res) => {
        wx.saveImageToPhotosAlbum({
          filePath: res.tempFilePath,
          success: () => {
            wx.hideLoading();
            wx.showToast({ title: '已保存到相册', icon: 'success' });
          },
          fail: (err) => {
            wx.hideLoading();
            if (err.errMsg && err.errMsg.includes('auth deny')) {
              wx.showModal({
                title: '需要授权',
                content: '请允许保存图片到相册',
                success: (modalRes) => {
                  if (modalRes.confirm) wx.openSetting();
                }
              });
            } else {
              wx.showToast({ title: '保存失败', icon: 'none' });
            }
          }
        });
      },
      fail: () => {
        wx.hideLoading();
        wx.showToast({ title: '下载失败', icon: 'none' });
      }
    });
  },

  goBack() {
    wx.navigateBack();
  },

  goToMyExchange() {
    wx.redirectTo({
      url: '/user-pages/my-exchange/my-exchange'
    });
  }
});
