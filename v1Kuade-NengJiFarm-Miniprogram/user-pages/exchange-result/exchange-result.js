const { api } = require('../../utils/api');
const share = require('../../utils/share');

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

    Promise.all([
      api.points.exchangeDetail(orderNo, { showLoading: false }),
      new Promise(resolve => setTimeout(resolve, 1000))
    ])
      .then(([data]) => {
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
    const baseUrl = getApp().globalData.baseUrl || 'https://api.nengjifarm.com';
    if (image.startsWith('/api/')) return baseUrl + image;
    return baseUrl + '/api/file/image/' + image;
  },

  saveQrcode() {
    const { qrcodeUrl } = this.data;
    if (!qrcodeUrl) {
      wx.showToast({ title: '暂无二维码', icon: 'none' });
      return;
    }

    this.setData({ loading: true });

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
                this.setData({ loading: false });
                wx.showToast({ title: '已保存到相册', icon: 'success' });
              },
              fail(err) {
                this.setData({ loading: false });
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
          this.setData({ loading: false });
          wx.showToast({ title: '保存失败', icon: 'none' });
        }
      });
      return;
    }

    // 普通 URL → downloadFile
    const that = this;
    wx.downloadFile({
      url: qrcodeUrl,
      success: (res) => {
        wx.saveImageToPhotosAlbum({
          filePath: res.tempFilePath,
          success: () => {
            that.setData({ loading: false });
            wx.showToast({ title: '已保存到相册', icon: 'success' });
          },
          fail: (err) => {
            that.setData({ loading: false });
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
        that.setData({ loading: false });
        wx.showToast({ title: '下载失败', icon: 'none' });
      }
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    if (this.data.orderNo) {
      this.setData({ loading: true });
      this.loadExchangeDetail(this.data.orderNo);
    }
  },

  goBack() {
    wx.navigateBack();
  },

  goToMyExchange() {
    wx.redirectTo({
      url: '/user-pages/my-exchange/my-exchange'
    });
  },

  /**
   * 取消兑换（仅待核销状态可取消）
   */
  cancelExchange() {
    wx.showModal({
      title: '取消兑换',
      content: '确定要取消该兑换吗？取消后积分将退回',
      success: (res) => {
        if (!res.confirm) return;

        this.setData({ loading: true });
        api.points.cancelExchange(this.data.orderNo, { showLoading: false })
          .then(() => {
            this.setData({ loading: false });
            wx.showToast({ title: '已取消', icon: 'success' });
            this.loadExchangeDetail(this.data.orderNo);
          })
          .catch((err) => {
            this.setData({ loading: false });
            const msg = err && err.message ? err.message : '取消失败，请重试';
            wx.showToast({ title: msg, icon: 'none' });
          });
      }
    });
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});
