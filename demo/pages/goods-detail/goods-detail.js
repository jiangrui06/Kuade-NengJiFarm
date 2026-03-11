Page({
  data: {
    // 商品详情
    goods: {},
    // 加载状态
    loading: true
  },

  onLoad: function (options) {
    const goodsId = options.id;
    if (goodsId) {
      this.getGoodsDetail(goodsId);
    }
  },

  // 获取商品详情
  getGoodsDetail: function(goodsId) {
    wx.showLoading({ title: '加载中...' });
    
    // 模拟API调用
    setTimeout(() => {
      // 根据商品id返回不同的商品详情
      let goodsData;
      
      switch(goodsId) {
        case '1':
          goodsData = {
            id: goodsId,
            name: '有机生菜',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20organic%20lettuce&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=lettuce%20field&image_size=portrait_4_3',
            description: '有机生菜，无农药，无化肥，自然生长，口感清脆，营养丰富，是健康饮食的理想选择。',
            weight: '500g',
            storage: '冷藏'
          };
          break;
        case '2':
          goodsData = {
            id: goodsId,
            name: '农家西红柿',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=tomato%20field&image_size=portrait_4_3',
            description: '农家种植的西红柿，自然成熟，酸甜可口，富含维生素C和番茄红素，是厨房必备的食材。',
            weight: '500g',
            storage: '常温'
          };
          break;
        case '3':
          goodsData = {
            id: goodsId,
            name: '新鲜黄瓜',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20cucumbers&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=cucumber%20field&image_size=portrait_4_3',
            description: '新鲜黄瓜，清脆爽口，含水量高，富含多种维生素和矿物质，适合凉拌、炒菜或直接食用。',
            weight: '500g',
            storage: '冷藏'
          };
          break;
        case '4':
          goodsData = {
            id: goodsId,
            name: '土猪肉',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20pork%20meat&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=pig%20farm&image_size=portrait_4_3',
            description: '农家散养土猪肉，肉质鲜美，肥瘦相间，营养丰富，是制作红烧肉、炖排骨的理想选择。',
            weight: '500g',
            storage: '冷冻'
          };
          break;
        case '5':
          goodsData = {
            id: goodsId,
            name: '农家土鸡',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20chicken&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=chicken%20farm&image_size=portrait_4_3',
            description: '农家散养土鸡，肉质紧实，味道鲜美，营养丰富，适合煲汤、红烧或清蒸。',
            weight: '1kg',
            storage: '冷冻'
          };
          break;
        case '6':
          goodsData = {
            id: goodsId,
            name: '土鸡蛋',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20eggs&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=chicken%20laying%20eggs&image_size=portrait_4_3',
            description: '农家散养土鸡下的蛋，蛋黄饱满，营养丰富，适合煎、炒、煮或做蛋羹。',
            weight: '10个',
            storage: '冷藏'
          };
          break;
        case '7':
          goodsData = {
            id: goodsId,
            name: '鸭蛋',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20duck%20eggs&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=ducks%20on%20farm&image_size=portrait_4_3',
            description: '农家散养鸭子下的蛋，个大饱满，营养丰富，适合腌制咸鸭蛋或做蛋羹。',
            weight: '10个',
            storage: '冷藏'
          };
          break;
        case '8':
          goodsData = {
            id: goodsId,
            name: '鹅蛋',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20goose%20eggs&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=geese%20on%20farm&image_size=portrait_4_3',
            description: '农家散养鹅下的蛋，个大饱满，营养丰富，适合做鹅蛋羹或煎蛋。',
            weight: '5个',
            storage: '冷藏'
          };
          break;
        case '9':
          goodsData = {
            id: goodsId,
            name: '新鲜牛奶',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20milk&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=cows%20on%20farm&image_size=portrait_4_3',
            description: '农场新鲜挤的牛奶，营养丰富，口感醇厚，适合直接饮用或制作奶制品。',
            weight: '500ml',
            storage: '冷藏'
          };
          break;
        case '10':
          goodsData = {
            id: goodsId,
            name: '农家酸奶',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20yogurt&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=yogurt%20making&image_size=portrait_4_3',
            description: '农家自制酸奶，口感醇厚，酸甜适中，富含益生菌，有助于消化。',
            weight: '500g',
            storage: '冷藏'
          };
          break;
        case '11':
          goodsData = {
            id: goodsId,
            name: '农家大米',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20rice&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=rice%20field&image_size=portrait_4_3',
            description: '农家种植的大米，颗粒饱满，口感香糯，营养丰富，是日常饮食的主食。',
            weight: '1kg',
            storage: '常温'
          };
          break;
        case '12':
          goodsData = {
            id: goodsId,
            name: '手工面条',
            price: 30,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20noodles&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=making%20noodles%20by%20hand&image_size=portrait_4_3',
            description: '手工制作的面条，口感劲道，营养丰富，适合做汤面、拌面或炒面。',
            weight: '500g',
            storage: '常温'
          };
          break;
        case '13':
          goodsData = {
            id: goodsId,
            name: '甜玉米500g',
            price: 69.99,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20sweet%20corn%20on%20the%20cob&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=corn%20field%20with%20fresh%20corn&image_size=portrait_4_3',
            description: '甜玉米是一种营养丰富的蔬菜，含有丰富的维生素、矿物质和膳食纤维。甜玉米口感清甜，适合煮食、烤制或制作沙拉。',
            weight: '500g',
            storage: '冷藏'
          };
          break;
        default:
          goodsData = {
            id: goodsId,
            name: '甜玉米500g',
            price: 69.99,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20sweet%20corn%20on%20the%20cob&image_size=square',
            detailImage: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=corn%20field%20with%20fresh%20corn&image_size=portrait_4_3',
            description: '甜玉米是一种营养丰富的蔬菜，含有丰富的维生素、矿物质和膳食纤维。甜玉米口感清甜，适合煮食、烤制或制作沙拉。',
            weight: '500g',
            storage: '冷藏'
          };
      }
      
      // 更新数据
      this.setData({
        goods: goodsData,
        loading: false
      });
      
      wx.hideLoading();
    }, 1000);
  },

  // 加入购物车
  addToCart: function() {
    wx.showToast({
      title: '已加入购物车',
      icon: 'success'
    });
  },

  // 立即购买
  buyNow: function() {
    wx.showToast({
      title: '购买功能开发中',
      icon: 'none'
    });
  }
});