const api = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    activity: {},
    loading: true,
    showQRCode: false,
    qrCodeUrl: '',
    orderId: '',
    hasVideo: false,
    swiperList: []
  },

  onLoad: function (options) {
    const activityId = options.id;
    const paid = options.paid === 'true';
    const orderId = options.orderId || '';

    this.activityId = activityId;
    this.paid = paid;
    this.orderId = orderId;

    if (activityId) {
      this.getActivityDetail(activityId, paid, orderId);
    }
  },

  onShow: function () {
    // 当页面显示时，只有在从支付页面返回时才重新加载活动数据
    // 避免每次页面显示都重新加载，提升用户体验
    if (this.activityId && this.paid) {
      this.getActivityDetail(this.activityId, this.paid, this.orderId);
      // 重置paid状态，避免下次页面显示时重复加载
      this.paid = false;
    }
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  // 获取活动图集（specImages → 活动图片）
  _getActivityImages: function (data) {
    if (!Array.isArray(data.specImages)) return [];
    return data.specImages.map(item => {
      if (typeof item === 'string') return this.processImageUrl(item);
      if (item && typeof item === 'object') return this.processImageUrl(item.image || item.url || item.src || '');
      return '';
    }).filter(Boolean);
  },

  // 检测是否为视频文件扩展名
  _isVideoUrl: function (url) {
    return /\.(mp4|mov|avi|mkv|wmv)$/i.test(String(url));
  },

  // 获取活动视频列表（videos 数组 → 多视频支持）
  _getActivityVideos: function (data) {
    if (Array.isArray(data.videos) && data.videos.length > 0) {
      return data.videos.map(v => {
        if (typeof v === 'string') return { url: this.processImageUrl(v), poster: '' };
        if (v && typeof v === 'object') {
          return {
            url: this.processImageUrl(v.url || ''),
            poster: v.poster ? this.processImageUrl(v.poster) : ''
          };
        }
        return null;
      }).filter(Boolean);
    }
    return [];
  },

  getActivityDetail: function (activityId, paid, orderId = '') {
    wx.showLoading({ title: '加载中...', mask: true });

    api.request({
      url: '/api/activity/detail',
      method: 'GET',
      data: {
        id: activityId
      },
      showLoading: false,
      skipAuthCheck: true
    })
      .then(data => {
        // 处理日期
        let dateStr = data.date || '';
        if (dateStr && !/\d{4}/.test(dateStr)) {
          const year = new Date().getFullYear();
        }

        // 轮播图（data.images）
        const carouselImages = (data.images || []).map(url => this.processImageUrl(url));

        // 视频地址：后端返回 data.video（单视频）和 data.videos（多视频数组）
        const rawVideoUrl = data.video || data.videoUrl || data.video_url || '';
        let videoUrl = '';
        if (rawVideoUrl) {
          videoUrl = String(rawVideoUrl).startsWith('http') ? String(rawVideoUrl) : this.processImageUrl(String(rawVideoUrl));
        }

        // 多视频数组（优先使用 videos 数组）
        const videos = this._getActivityVideos(data);
        // 如果 videos 数组为空但旧字段有视频 URL，兜底为单视频数组
        if (videos.length === 0 && videoUrl) {
          videos.push({ url: videoUrl, poster: carouselImages[0] || '' });
        }

        // 构建统一轮播数组
        let swiperList = [];
        if (Array.isArray(data.carouselMedia) && data.carouselMedia.length > 0) {
          // 优先使用后端 carouselMedia（支持交叉排序）
          swiperList = data.carouselMedia.map(item => ({
            type: item.type === 'video' ? 'video' : 'image',
            image: this.processImageUrl(item.url || item.image || ''),
            poster: item.type === 'video' ? (item.poster || '') : ''
          }));
        } else {
          // 降级：从 images + videos 拼合（先图片后视频）
          swiperList = [
            ...carouselImages.map(url => ({ type: 'image', image: url })),
            ...videos.map(v => ({ type: 'video', image: v.url, poster: v.poster }))
          ];
        }

        const processedActivity = {
          ...data,
          image: this.processImageUrl(data.image),
          carouselImages,
          images: this._getActivityImages(data),
          videoUrl: videoUrl,
          videos: videos,
          price: typeof data.price === 'string' ? data.price.replace(/[¥￥]/g, '') : data.price,
          date: dateStr
        };

        this.setData({
          activity: processedActivity || {},
          swiperList: swiperList,
          loading: false,
          orderId: orderId || this.data.orderId,
          hasVideo: !!videoUrl || videos.length > 0
        });

        // 如果支付成功，显示二维码
        if (paid) {
          this.showQRCode(orderId);
        }
      })
      .catch(err => {
        wx.showToast({
          title: err.message || '活动详情加载失败',
          icon: 'none'
        });
        this.setData({ loading: false });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  // 显示二维码
  showQRCode: function (orderId = '') {
    const targetOrderId = orderId || this.data.orderId;
    if (!targetOrderId) {
      wx.showToast({
        title: '缺少订单ID',
        icon: 'none'
      });
      return;
    }

    api.order.getQrcode(targetOrderId)
      .then(data => {
        this.setData({
          qrCodeUrl: data.qrCodeUrl || '',
          showQRCode: true
        });
      })
      .catch(err => {
        wx.showToast({
          title: '获取二维码失败',
          icon: 'none'
        });
      });
  },

  registerActivity: function () {
    // 登录检查
    const { checkLogin } = require('../../utils/api');
    if (!checkLogin()) return;

    const remainingSlots = this.data.activity.remainingSlots || 0;
    if (remainingSlots <= 0) {
      wx.showToast({
        title: '已售罄',
        icon: 'none'
      });
      return;
    }

    this.showRegisterModal(remainingSlots);
  },

  // 显示报名弹窗
  showRegisterModal: function (remainingSlots) {
    const priceText = this.data.activity.price || '0';
    const that = this;
    wx.showModal({
      title: `当前剩余 ${remainingSlots} 个名额，${priceText} 元/人`,
      editable: true,
      placeholderText: '请输入购票张数',
      success: function (res) {
        if (!res.confirm) {
          return;
        }

        if (!res.content || res.content.trim() === '') {
          wx.showToast({
            title: '请输入购票张数',
            icon: 'none'
          });
          return;
        }

        const inputContent = res.content.trim();
        // 检查是否包含小数点或其他非数字字符
        if (!/^\d+$/.test(inputContent)) {
          wx.showToast({
            title: '请输入整数张数',
            icon: 'none'
          });
          return;
        }

        const tickets = parseInt(inputContent, 10);
        if (!(tickets > 0)) {
          wx.showToast({
            title: '请输入有效的票数',
            icon: 'none'
          });
          return;
        }

        if (tickets > remainingSlots) {
          wx.showToast({
            title: `购票数量不能超过剩余 ${remainingSlots} 个名额`,
            icon: 'none'
          });
          return;
        }

        wx.showLoading({ title: '下单中...', mask: true })

        api.request({
          url: `/api/activity/${that.data.activity.id}/register`,
          method: 'POST',
          data: {
            tickets: tickets
          },
          showLoading: false
        })
          .then(orderData => {
            wx.hideLoading();
            const orderNo = orderData.orderNo || orderData.orderId || orderData.id;
            if (!orderNo) {
              wx.showToast({ title: '下单失败', icon: 'none' });
              return;
            }
            // 跳转支付
            wx.redirectTo({
              url: `/user-pages/pay/pay?orderNo=${orderNo}&type=activity&activityId=${that.data.activity.id}`
            });
          })
          .catch(err => {
            wx.hideLoading();
            wx.showToast({
              title: err.message || '下单失败',
              icon: 'none'
            });
          });
      }
    });
  },

  hideQRCode: function () {
    this.setData({
      showQRCode: false
    });
  },

  // 预览图片（按区域：轮播图 / 活动图集）
  previewImage: function (e) {
    const current = e.currentTarget.dataset.src;
    const group = e.currentTarget.dataset.group || 'gallery';
    let urls = [];
    if (group === 'swiper') {
      urls = this.data.swiperList.filter(item => item.type === 'image').map(item => item.image);
    } else {
      urls = this.data.activity.images || [];
    }
    if (urls.length === 0) {
      urls = this.data.activity.images || [];
    }
    wx.previewImage({
      current: current,
      urls: urls
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    if (this.data.activity && this.data.activity.id) {
      this.getActivityDetail(this.data.activity.id, false, this.data.orderId);
    }
    // 刷新完成后停止下拉刷新
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});

