const api = require('../../utils/api');

Page({
  data: {
    acreDetail: {},
    swiperList: [],
    hasVideo: false
  },

  onLoad(options) {
    const id = options.id;
    this.loadAcreDetail(id);
  },

  processImageUrl(imageUrl) {
    if (!imageUrl) return '';

    const cleaned = String(imageUrl).replace(/[`\s]/g, '');
    if (cleaned.startsWith('http://') || cleaned.startsWith('https://')) {
      return cleaned.replace('http://192.168.101.47', 'http://192.168.101.47');
    }

    return 'http://192.168.101.47' + cleaned;
  },

  loadAcreDetail(id) {
    wx.showLoading({ title: '加载中...' });

    api.api.acre.getDetail(id)
      .then((acreData) => {
        wx.hideLoading();

        const detail = acreData?.acreDetail || {};
        const cleanData = {
          ...detail,
          image: this.processImageUrl(detail.image),
          longExampleImage: this.processImageUrl(detail.longExampleImage),
          swiperList: (detail.swiperList || []).map(item => ({
            ...item,
            image: this.processImageUrl(item.image)
          })),
          // 活动图片风格：images 优先
          images: (detail.images || []).map(image => this.processImageUrl(image)),
          longExampleImages: (detail.longExampleImages || []).map(image => this.processImageUrl(image)),
          longExampleImageList: (detail.longExampleImageList || []).map(image => this.processImageUrl(image)),
          bottomImages: (detail.bottomImages || []).map(image => this.processImageUrl(image))
        };

        // 视频处理：优先用后端返回的 videoUrl，没有则不显示
        let videoUrl = '';
        if (detail.videoUrl) {
          videoUrl = String(detail.videoUrl).startsWith('http')
            ? detail.videoUrl
            : this.processImageUrl(detail.videoUrl);
        }

        const hasVideo = !!videoUrl;
        
        this.setData({
          acreDetail: {
            ...cleanData,
            videoUrl,
            remainingAcres: cleanData.remainingAcres,
            soldAcres: cleanData.soldAcres,
            longExampleImage: cleanData.longExampleImage,
            images: cleanData.images,
            bottomImages: cleanData.bottomImages,
            longExampleImages: cleanData.longExampleImages,
            longExampleImageList: cleanData.longExampleImageList,
            price: typeof cleanData.price === 'string'
              ? cleanData.price.replace(/[^0-9.]/g, '')
              : cleanData.price
          },
          swiperList: cleanData.swiperList,
          hasVideo: hasVideo
        });
      })
      .catch(() => {
        wx.hideLoading();
        wx.showToast({
          title: '加载失败，请重试',
          icon: 'none'
        });
      });
  },

  contactService() {
    wx.showModal({
      title: '客服',
      content: '手机号：15876534944\n微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  // 预览轮播图（单张）
  previewImage(e) {
    const url = e.currentTarget.dataset.url;
    if (url) {
      wx.previewImage({
        current: url,
        urls: [url]
      });
    }
  },

  // 预览示例图列表
  previewExampleImage(e) {
    const { acreDetail } = this.data;
    // 获取所有示例图 URL 列表（优先级：images > bottomImages > longExampleImages > longExampleImageList > 单张）
    let imageList = [];
    if (acreDetail.images && acreDetail.images.length > 0) {
      imageList = acreDetail.images;
    } else if (acreDetail.bottomImages && acreDetail.bottomImages.length > 0) {
      imageList = acreDetail.bottomImages;
    } else if (acreDetail.longExampleImages && acreDetail.longExampleImages.length > 0) {
      imageList = acreDetail.longExampleImages;
    } else if (acreDetail.longExampleImageList && acreDetail.longExampleImageList.length > 0) {
      imageList = acreDetail.longExampleImageList;
    } else if (acreDetail.longExampleImage) {
      imageList = [acreDetail.longExampleImage];
    }

    if (imageList.length === 0) return;

    const currentUrl = e.currentTarget.dataset.url || imageList[0];
    wx.previewImage({
      current: currentUrl,
      urls: imageList
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    console.log('下拉刷新认购详情');
    if (this.data.acreDetail && this.data.acreDetail.id) {
      this.loadAcreDetail(this.data.acreDetail.id);
    }
    // 刷新完成后停止下拉刷新
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  },

  confirmPurchase() {
    const remainingAcres = Number(this.data.acreDetail.remainingAcres || 0);
    if (remainingAcres <= 0) {
      wx.showToast({ title: '已售完', icon: 'none' });
      return;
    }

    wx.showModal({
      title: `当前剩余 ${remainingAcres} 亩`,
      editable: true,
      placeholderText: '请输入购买亩数',
      success: function (res) {
        if (!res.confirm) return;

        if (!res.content || !res.content.trim()) {
          wx.showToast({ title: '请输入购买亩数', icon: 'none' });
          return;
        }

        const inputContent = res.content.trim();
        // 检查是否包含小数点或其他非数字字符
        if (!/^\d+$/.test(inputContent)) {
          wx.showToast({
            title: '请输入整数亩数',
            icon: 'none'
          });
          return;
        }

        const acres = parseInt(inputContent, 10);
        if (!(acres > 0)) {
          wx.showToast({ title: '请输入有效亩数', icon: 'none' });
          return;
        }

        if (acres > remainingAcres) {
          wx.showToast({ title: `购买数量不能超过剩余 ${remainingAcres} 亩`, icon: 'none' });
          return;
        }

        const unitPrice = parseFloat(String(this.data.acreDetail.price || 0).replace(/[^0-9.]/g, '')) || 0;
        const totalPrice = unitPrice * acres;

        wx.showLoading({ title: '下单中...' });
        api.request({
          url: `/api/acres/${this.data.acreDetail.id}/adopt`,
          method: 'POST',
          data: {
            quantity: acres,
            acres
          },
          showLoading: false
        })
          .then((orderData) => {
            const orderId = orderData.orderId || orderData.id;
            if (!orderId) {
              wx.showToast({ title: '创建订单失败', icon: 'none' });
              return;
            }

            wx.navigateTo({
              url: `/user-pages/pay/pay?orderId=${orderId}&totalPrice=${totalPrice.toFixed(2)}`
            });
          })
          .catch((err) => {
            console.error('创建认购订单失败:', err);
            wx.showToast({ title: '创建订单失败', icon: 'none' });
          })
          .finally(() => {
            wx.hideLoading();
          });
      }.bind(this)
    });
  }
});
