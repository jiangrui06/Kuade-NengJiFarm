const api = require('../../utils/api');

Page({
  data: {
    showCategory: false,
    showCategoryView: false,
    currentCategory: '',
    swiperList: [],
    categories: [],
    todayGoods: [],
    hotGoods: [],
    categoryGoods: {},
    currentCategoryGoods: [],
    loading: true
  },

  onLoad: function() {
    this.getFarmGoodsData();
  },

  getFarmGoodsData: function() {
    wx.showLoading({ title: '加载中...', mask: true });

    api.request({
      url: '/api/farm-goods/index',
      method: 'GET'
    })
      .then(data => {
        const categories = data.categories || [];
        const currentCategory = categories.length > 0 ? categories[0].id : '';

        this.setData({
          swiperList: data.swiperList || [],
          categories,
          todayGoods: data.todayGoods || [],
          hotGoods: data.hotGoods || [],
          currentCategory,
          currentCategoryGoods: [],
          loading: false
        });

        if (currentCategory) {
          this.loadCategoryGoods(currentCategory);
        }
      })
      .catch(err => {
        this.setData({ loading: false });
        wx.showToast({
          title: err.message || '农场优选加载失败',
          icon: 'none'
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  loadCategoryGoods: function(categoryId) {
    api.request({
      url: '/api/farm-goods/category',
      method: 'GET',
      data: {
        categoryId,
        page: 1,
        pageSize: 20
      }
    })
      .then(data => {
        this.setData({
          currentCategory: categoryId,
          currentCategoryGoods: data.goodsList || []
        });
      })
      .catch(err => {
        wx.showToast({
          title: err.message || '分类商品加载失败',
          icon: 'none'
        });
      });
  },

  search: function() {
    wx.showToast({
      title: '当前页面未接入搜索输入框',
      icon: 'none'
    });
  },

  toggleCategory: function() {
    const nextState = !this.data.showCategoryView;
    this.setData({
      showCategory: nextState,
      showCategoryView: nextState
    });
  },

  selectCategory: function(e) {
    const categoryId = e.currentTarget.dataset.id;
    this.setData({
      showCategory: false,
      showCategoryView: true
    });
    this.loadCategoryGoods(categoryId);
  },

  getCurrentCategoryName: function() {
    const category = this.data.categories.find(item => item.id === this.data.currentCategory);
    return category ? category.name : '商品分类';
  },

  viewMore: function() {
    this.setData({
      showCategory: false,
      showCategoryView: true
    });

    if (!this.data.currentCategory && this.data.categories.length > 0) {
      this.loadCategoryGoods(this.data.categories[0].id);
    }
  },

  viewGoodsDetail: function(e) {
    const goodsId = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/pages/goods-detail/goods-detail?id=' + goodsId
    });
  }
});
