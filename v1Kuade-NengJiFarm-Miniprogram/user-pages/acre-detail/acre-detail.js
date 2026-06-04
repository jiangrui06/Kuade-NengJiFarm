const api = require('../../utils/api');
const share = require('../../utils/share');

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
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  loadAcreDetail(id) {
    wx.showLoading({ title: '加载中...' });

    // 认购商品数据已迁移到商品表，使用 /api/goods/detail 接口获取详情
    // 认购商品使用 type=goods 参数（因为数据已迁移到商品表）
    const requestUrl = `/api/goods/detail?goodsId=${id}&type=goods`;

    api.goods.getDetail(id, 'goods')
      .then((goodsData) => {
        wx.hideLoading();

        const detail = goodsData || {};

        // 处理商品数据，适配 /api/goods/detail 接口返回的数据结构
        // 认购商品详情接口返回的数据结构与普通商品完全一致
        const cleanData = {
          id: detail.id,
          name: detail.name,
          price: typeof detail.price === 'string' ? detail.price.replace(/[^0-9.]/g, '') : detail.price,
          originalPrice: detail.originalPrice,
          image: this.processImageUrl(detail.image || detail.mainImage || detail.main_image),
          description: detail.description || detail.desc,
          spec: detail.spec || '',
          stock: detail.stock,
          sold: detail.sold,
          tags: detail.tags || []
        };

        // 处理轮播图
        const swiperList = (detail.swiperList || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image)
        }));

        this.setData({
          acreDetail: cleanData,
          swiperList: swiperList,
          hasVideo: false
        });
      })
      .catch((err) => {
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
      wx.showToast({ title: '已售罄', icon: 'none' });
      return;
    }

    const that = this;
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

        const unitPrice = parseFloat(String(that.data.acreDetail.price || 0).replace(/[^0-9.]/g, '')) || 0;
        const totalPrice = unitPrice * acres;

        wx.showLoading({ title: '下单中...' });
        api.request({
          url: `/api/acres/${that.data.acreDetail.id}/adopt`,
          method: 'POST',
          data: {
            quantity: acres,
            acres
          },
          showLoading: false
        })
          .then((orderData) => {
            wx.hideLoading();
            const orderNo = orderData.orderNo || orderData.orderId || orderData.id;
            if (!orderNo) {
              wx.showToast({ title: '创建订单失败', icon: 'none' });
              return;
            }
            // 跳转到支付页面
            wx.redirectTo({
              url: `/user-pages/pay/pay?orderNo=${orderNo}&totalPrice=${totalPrice}&type=acre`
            });
          })
          .catch((err) => {
            wx.hideLoading();
            wx.showToast({ title: err.message || '下单失败', icon: 'none' });
          });
      }
    });
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});

