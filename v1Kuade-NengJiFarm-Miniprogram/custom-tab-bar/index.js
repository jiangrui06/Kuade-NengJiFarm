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

// 员工 tab 配置（工作台 + 我的）
const STAFF_TABS = [
  {
    path: '/staff-pages/staff-home/staff-home',
    text: '工作台',
    icon: '/images/rroomm.png',
    selectedIcon: '/images/rroom.png'
  },
  {
    path: '/pages/profile/profile',
    text: '我的',
    icon: '/images/user.png',
    selectedIcon: '/images/user2.png'
  }
];

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

      // 员工工作台在 staff-pages 里是普通页面，不能用 switchTab，用 reLaunch 切换
      if (path.includes('/staff-home/')) {
        wx.redirectTo({ url: path });
        return;
      }

      wx.switchTab({ url: path });
    }
  }
});

