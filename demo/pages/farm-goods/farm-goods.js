const api = require('../../utils/api');

const FALLBACK_CATEGORIES = [
  { id: 'all', name: '全部商品', color: '#4CAF50', icon: '全' },
  { id: 'new', name: '新品上市', color: '#FF9800', icon: '新' }
];

Page({
  data: {
    showCategory: false,
    showCategoryView: false,
    currentCategory: 'all',
    swiperList: [],
    categories: FALLBACK_CATEGORIES,
    todayGoods: [],
    hotGoods: [],
    currentCategoryGoods: [],
    loading: true,
    loadingMore: false,
    loadingTodayMore: false,
    page: 1,
    todayPage: 1,
    pageSize: 6,
    todayPageSize: 4,
    hasMore: true,
    hasTodayMore: true,
    goodsCache: {}
  },

  onLoad() {
    this.getFarmGoodsData();
  },

  buildFallbackSwiperList(items, serverSwiperList) {
    if (Array.isArray(serverSwiperList) && serverSwiperList.length > 0) {
      return serverSwiperList
        .filter(item => item && item.image)
        .map((item, index) => ({
          id: item.id || index + 1,
          image: item.image
        }));
    }

    const images = (items || [])
      .filter(item => item && item.image)
      .slice(0, 3)
      .map((item, index) => ({
        id: item.id || index + 1,
        image: item.image
      }));

    return images;
  },

  getFarmGoodsData() {
    wx.showLoading({ title: '加载中...', mask: true });

    this.setData({
      currentCategory: 'all',
      currentCategoryGoods: [],
      todayGoods: [],
      loading: true,
      loadingMore: false,
      loadingTodayMore: false,
      page: 1,
      todayPage: 1,
      hasMore: true,
      hasTodayMore: true
    });

    api.request({
      url: '/api/DemoApi/goods',
      method: 'GET',
      data: {
        category: 'all'
      }
    })
      .then((data) => {
        const items = Array.isArray(data.items) ? data.items : [];
        const firstPageGoods = this.sliceGoodsPage(items, 1);
        const swiperList = this.buildFallbackSwiperList(items, data.swiperList);
        const todayFirstPage = items.slice(0, this.data.todayPageSize);

        this.setData({
          swiperList,
          categories: Array.isArray(data.categories) && data.categories.length ? data.categories : FALLBACK_CATEGORIES,
          todayGoods: todayFirstPage,
          hotGoods: items.slice(0, 4),
          currentCategory: data.category || 'all',
          currentCategoryGoods: firstPageGoods,
          goodsCache: {
            ...this.data.goodsCache,
            all: items,
            today: items
          },
          loading: false,
          page: 1,
          todayPage: 1,
          hasMore: items.length > this.data.pageSize,
          hasTodayMore: items.length > this.data.todayPageSize
        });
      })
      .catch((err) => {
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

  sliceGoodsPage(goodsList, page) {
    const end = page * this.data.pageSize;
    return goodsList.slice(0, end);
  },

  applyCategoryPage(categoryId, page) {
    const categoryGoods = this.data.goodsCache[categoryId] || [];
    const start = (page - 1) * this.data.pageSize;
    const end = page * this.data.pageSize;
    const newItems = categoryGoods.slice(start, end);

    this.setData({
      currentCategory: categoryId,
      currentCategoryGoods: page === 1
        ? newItems
        : [...this.data.currentCategoryGoods, ...newItems],
      page,
      hasMore: categoryGoods.length > end,
      loading: false,
      loadingMore: false
    });
  },

  fetchCategoryGoods(categoryId) {
    return api.request({
      url: '/api/DemoApi/goods',
      method: 'GET',
      data: {
        category: categoryId
      }
    }).then((data) => Array.isArray(data.items) ? data.items : []);
  },

  loadCategoryGoods(categoryId, isLoadMore = false) {
    const nextPage = isLoadMore ? this.data.page + 1 : 1;
    const cachedGoods = this.data.goodsCache[categoryId];

    if (cachedGoods) {
      if (isLoadMore) {
        this.setData({ loadingMore: true });
      } else {
        this.setData({ loading: true });
      }

      this.applyCategoryPage(categoryId, nextPage);
      return;
    }

    if (isLoadMore) {
      this.setData({ loadingMore: true });
    } else {
      this.setData({
        loading: true,
        currentCategory: categoryId,
        currentCategoryGoods: [],
        page: 1,
        hasMore: true
      });
    }

    this.fetchCategoryGoods(categoryId)
      .then((goodsList) => {
        this.setData({
          goodsCache: {
            ...this.data.goodsCache,
            [categoryId]: goodsList
          }
        });

        this.applyCategoryPage(categoryId, nextPage);
      })
      .catch((err) => {
        this.setData({ loading: false, loadingMore: false });
        wx.showToast({
          title: err.message || '分类商品加载失败',
          icon: 'none'
        });
      });
  },

  search() {
    wx.showToast({
      title: '当前页面未接入搜索输入框',
      icon: 'none'
    });
  },

  toggleCategory() {
    const nextState = !this.data.showCategoryView;
    this.setData({
      showCategory: nextState,
      showCategoryView: nextState
    });
  },

  selectCategory(e) {
    const categoryId = e.currentTarget.dataset.id;
    this.setData({
      showCategory: false,
      showCategoryView: true
    });
    this.loadCategoryGoods(categoryId);
  },

  getCurrentCategoryName() {
    const category = this.data.categories.find((item) => item.id === this.data.currentCategory);
    return category ? category.name : '商品分类';
  },

  viewMore() {
    this.setData({
      showCategory: false,
      showCategoryView: true
    });

    this.loadCategoryGoods(this.data.currentCategory || 'all');
  },

  viewGoodsDetail(e) {
    const goodsId = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/pages/goods-detail/goods-detail?id=' + goodsId
    });
  },

  onReachBottom() {
    if (this.data.showCategoryView) {
      if (!this.data.loadingMore && this.data.hasMore) {
        this.loadCategoryGoods(this.data.currentCategory, true);
      }
    } else {
      if (!this.data.loadingTodayMore && this.data.hasTodayMore) {
        this.loadMoreTodayGoods();
      }
    }
  },

  onCategoryScrollToLower() {
    if (!this.data.showCategoryView || this.data.loadingMore || !this.data.hasMore) {
      return;
    }

    this.loadCategoryGoods(this.data.currentCategory, true);
  },

  onTodayGoodsScrollToLower() {
    if (this.data.showCategoryView || this.data.loadingTodayMore || !this.data.hasTodayMore) {
      return;
    }

    this.loadMoreTodayGoods();
  },

  loadMoreTodayGoods() {
    this.setData({ loadingTodayMore: true });

    const todayGoods = this.data.goodsCache.today || [];
    const currentPage = this.data.todayPage;
    const nextPage = currentPage + 1;
    const start = currentPage * this.data.todayPageSize;
    const end = nextPage * this.data.todayPageSize;
    const newItems = todayGoods.slice(start, end);

    setTimeout(() => {
      this.setData({
        todayGoods: [...this.data.todayGoods, ...newItems],
        todayPage: nextPage,
        hasTodayMore: todayGoods.length > end,
        loadingTodayMore: false
      });
    }, 500);
  }
});