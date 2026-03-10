Page({
  data: {
    profileItems: [
      { id: 1, name: '个人信息' },
      { id: 2, name: '订单管理' },
      { id: 3, name: '收货地址' },
      { id: 4, name: '设置' }
    ]
  },

  onLoad: function () {
    console.log('个人中心加载')
  }
})