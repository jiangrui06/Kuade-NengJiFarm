Page({
  data: {
    // 当前选中的选项卡
    activeTab: 'all',
    // 活动数据
    activities: {
      all: [
        {
          id: 1,
          title: '农家研学活动报名中',
          price: '门票: 10-20 ¥',
          date: '2025.2.25-2025.3.6',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20playing%20football%20on%20farm&image_size=landscape_16_9'
        },
        {
          id: 2,
          title: '采摘活动报名中',
          price: '门票: 10-50 ¥',
          date: '2025.2.25-2025.3.6',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20lettuce%20field&image_size=landscape_16_9'
        },
        {
          id: 3,
          title: '草莓采摘体验',
          price: '门票: 30 ¥/人',
          date: '2025.3.1-2025.4.30',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=strawberry%20picking&image_size=landscape_16_9'
        },
        {
          id: 4,
          title: '葡萄采摘节',
          price: '门票: 50 ¥/人',
          date: '2025.7.1-2025.8.31',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=grape%20picking&image_size=landscape_16_9'
        },
        {
          id: 5,
          title: '农场露营体验',
          price: '费用: 120 ¥/晚',
          date: '2025.4.1-2025.10.31',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=farm%20camping%20tent&image_size=landscape_16_9'
        },
        {
          id: 6,
          title: '篝火露营晚会',
          price: '费用: 180 ¥/人',
          date: '2025.5.1-2025.9.30',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=camping%20with%20campfire&image_size=landscape_16_9'
        }
      ],
      picking: [
        {
          id: 2,
          title: '采摘活动报名中',
          price: '门票: 10-50 ¥',
          date: '2025.2.25-2025.3.6',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20lettuce%20field&image_size=landscape_16_9'
        },
        {
          id: 3,
          title: '草莓采摘体验',
          price: '门票: 30 ¥/人',
          date: '2025.3.1-2025.4.30',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=strawberry%20picking&image_size=landscape_16_9'
        },
        {
          id: 4,
          title: '葡萄采摘节',
          price: '门票: 50 ¥/人',
          date: '2025.7.1-2025.8.31',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=grape%20picking&image_size=landscape_16_9'
        }
      ],
      camping: [
        {
          id: 5,
          title: '农场露营体验',
          price: '费用: 120 ¥/晚',
          date: '2025.4.1-2025.10.31',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=farm%20camping%20tent&image_size=landscape_16_9'
        },
        {
          id: 6,
          title: '篝火露营晚会',
          price: '费用: 180 ¥/人',
          date: '2025.5.1-2025.9.30',
          image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=camping%20with%20campfire&image_size=landscape_16_9'
        }
      ]
    }
  },

  onLoad: function () {
    console.log('活动中心加载')
  },

  // 切换选项卡
  switchTab: function (e) {
    const tab = e.currentTarget.dataset.tab
    this.setData({
      activeTab: tab
    })
  }
})