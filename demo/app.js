App({
  onLaunch: function () {
    console.log('小程序启动')
    // 获取图标列表并设置tabBar图标
    this.getIconsAndSetTabBar()
  },
  
  onShow: function () {
    console.log('小程序显示')
    // 再次获取图标列表并设置tabBar图标，确保图标始终最新
    this.getIconsAndSetTabBar()
  },
  
  // 获取图标列表并设置tabBar图标
  getIconsAndSetTabBar: function () {
    console.log('开始获取图标列表')
    const api = require('./utils/api')
    
    api.api.file.getImages()
      .then(data => {
        console.log('获取图标列表成功:', data)
        
        // 检查是否有所需的图标文件
        const files = data.files || []
        console.log('可用图标文件:', files)
        
        // 下载图标到本地并设置tabBar
        this.downloadAndSetTabBarIcon(0, files)
      })      .catch(err => {
        console.error('获取图标列表失败:', err)
        // 直接尝试下载图标，不依赖API返回的文件列表
        this.downloadAndSetTabBarIcon(0, ['rroom.png', 'tyyyyyyyy.png', 'tyyyyy.png', 'user.png', 'user2.png', 'shoppingg.png'])
      })
  },
  
  // 下载图标到本地并设置tabBar
  downloadAndSetTabBarIcon: function (index, files) {
    console.log('开始下载和设置图标，当前索引:', index)
    const tabBarConfig = [
      {
        index: 0,
        name: '首页',
        icon: files.includes('rroom.png') ? 'rroom.png' : '',
        selectedIcon: files.includes('rroom.png') ? 'rroom.png' : ''
      },
      {
        index: 1,
        name: '活动',
        icon: files.includes('tyyyyyyyy.png') ? 'tyyyyyyyy.png' : '',
        selectedIcon: files.includes('tyyyyy.png') ? 'tyyyyy.png' : (files.includes('ty.png') ? 'ty.png' : '')
      },
      {
        index: 2,
        name: '购物车',
        icon: files.includes('shoppingg.png') ? 'shoppingg.png' : '',
        selectedIcon: files.includes('shoppingg.png') ? 'shoppingg.png' : ''
      },
      {        index: 3,        name: '我的',        icon: files.includes('user.png') ? 'user.png' : '',        selectedIcon: files.includes('user2.png') ? 'user2.png' : ''      }
    ]
    
    if (index >= tabBarConfig.length) {
      console.log('所有图标处理完成')
      return
    }
    
    const config = tabBarConfig[index]
    console.log('处理图标:', config.name, '图标:', config.icon, '选中图标:', config.selectedIcon)
    
    if (config.icon && config.selectedIcon) {
      // 下载图标文件
      console.log('开始下载图标:', `http://192.168.203.56/api/file/image/${config.icon}`)
      wx.downloadFile({
        url: `http://192.168.203.56/api/file/image/${config.icon}`,
        success: (res) => {
          console.log('下载图标成功:', config.icon, '状态码:', res.statusCode)
          if (res.statusCode === 200) {
            const iconPath = res.tempFilePath
            console.log('图标临时路径:', iconPath)
            
            // 下载选中状态图标
            console.log('开始下载选中图标:', `http://192.168.203.56/api/file/image/${config.selectedIcon}`)
            wx.downloadFile({
              url: `http://192.168.203.56/api/file/image/${config.selectedIcon}`,
              success: (res2) => {
                console.log('下载选中图标成功:', config.selectedIcon, '状态码:', res2.statusCode)
                if (res2.statusCode === 200) {
                  const selectedIconPath = res2.tempFilePath
                  console.log('选中图标临时路径:', selectedIconPath)
                  
                  // 检查当前页面是否是TabBar页面
                  wx.getSetting({
                    success: (res) => {
                      // 尝试获取当前页面路径
                      try {
                        const pages = getCurrentPages();
                        const currentPage = pages[pages.length - 1];
                        const currentPagePath = currentPage.route;
                        console.log('当前页面路径:', currentPagePath);
                        
                        // TabBar页面路径列表
                        const tabBarPages = ['pages/index/index', 'pages/activity/activity', 'pages/cart/cart', 'pages/profile/profile'];
                        
                        // 只有在TabBar页面时才设置TabBar图标
                        if (tabBarPages.includes(currentPagePath)) {
                          // 设置tabBar图标
                          console.log('开始设置tabBar图标:', config.index, iconPath, selectedIconPath)
                          wx.setTabBarItem({
                            index: config.index,
                            iconPath: iconPath,
                            selectedIconPath: selectedIconPath,
                            success: () => {
                              console.log('设置tabBar图标成功:', config.name)
                            },
                            fail: (err) => {
                              console.error('设置tabBar图标失败:', config.name, err)
                            }
                          });
                        } else {
                          console.log('当前页面不是TabBar页面，跳过设置TabBar图标');
                        }
                      } catch (e) {
                        console.error('获取当前页面路径失败:', e);
                      }
                    }
                  });
                }
                
                // 处理下一个图标
                this.downloadAndSetTabBarIcon(index + 1, files)
              },
              fail: (err) => {
                console.error('下载选中图标失败:', config.selectedIcon, err)
                // 处理下一个图标
                this.downloadAndSetTabBarIcon(index + 1, files)
              }
            })
          } else {
            console.error('下载图标失败:', config.icon, '状态码:', res.statusCode)
            // 处理下一个图标
            this.downloadAndSetTabBarIcon(index + 1, files)
          }
        },
        fail: (err) => {
          console.error('下载图标失败:', config.icon, err)
          // 处理下一个图标
          this.downloadAndSetTabBarIcon(index + 1, files)
        }
      })
    } else {
      console.log('跳过图标处理:', config.name, '原因: 图标文件不存在')
      // 处理下一个图标
      this.downloadAndSetTabBarIcon(index + 1, files)
    }
  },
  
  globalData: {
    userInfo: null
  }
})