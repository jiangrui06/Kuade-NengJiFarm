// 普通用户 tab 配置
const USER_TABS = [
  {
    path: '/pages/index/index',
    text: '首页',
    icon: '/images/rroomm.png',
    selectedIcon: '/images/rroom.png'
  },
  {
    path: '/pages/activity/activity',
    text: '活动',
    icon: '/images/tyyyyy.png',
    selectedIcon: '/images/tyyyyyyyy.png'
  },
  {
    path: '/pages/cart/cart',
    text: '购物车',
    icon: '/images/shoppingg.png',
    selectedIcon: '/images/tyyy.png'
  },
  {
    path: '/pages/profile/profile',
    text: '我的',
    icon: '/images/user.png',
    selectedIcon: '/images/user2.png'
  }
];

// 员工也使用和普通用户相同的 tab 配置（首页/活动/购物车/我的）
const STAFF_TABS = USER_TABS;

Component({
  data: {
    tabs: USER_TABS,
    selected: 0
  },

  methods: {
    init() {
      const role = wx.getStorageSync('user_role') || 'user';
      const isStaff = role === 'staff';
      var tabs = isStaff ? STAFF_TABS : USER_TABS;
      
      // 获取当前页面路径，确定选中项
      var pages = getCurrentPages();
      var currentPage = pages[pages.length - 1];
      var currentRoute = currentPage ? ('/' + currentPage.route) : '';
      
      var selectedIndex = 0;
      for (var i = 0; i < tabs.length; i++) {
        if (tabs[i].path === currentRoute) {
          selectedIndex = i;
          break;
        }
      }

      this.setData({
        tabs: tabs,
        selected: selectedIndex
      });
    },

    switchTab(e) {
      var data = e.currentTarget.dataset;
      var index = data.index;
      var path = data.path;

      if (index === this.data.selected) return;

      // 点击"我的"时，未登录直接跳转到登录页面
      if (path === '/pages/profile/profile') {
        const token = wx.getStorageSync('token');
        if (!token) {
          wx.showToast({ title: '请先登录', icon: 'none' });
          setTimeout(() => {
            wx.navigateTo({ url: '/pages/login/login' });
          }, 500);
          return;
        }
      }

      wx.switchTab({ url: path });
    }
  }
});