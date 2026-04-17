Page({
  data: {
    farmInfo: null,
    loading: true,
    // 默认农场主图
    defaultMainImage: 'http://192.168.203.56/api/file/image/farm_0000000000007.jpg'
  },

  onLoad: function (options) {
    console.log('农场介绍页面加载');
    // 直接使用默认农场信息
    this.setData({
      farmInfo: {
        mainImage: this.data.defaultMainImage,
        introduction: '能记家庭农场致力于提供绿色、健康、有机的农产品，采用传统种植方式，不使用化学农药和化肥，确保产品的品质和安全。',
        philosophy: '我们坚持"自然、健康、可持续"的发展理念，致力于为消费者提供最优质的农产品，同时保护生态环境，实现农业的可持续发展。',
        contact: {
          address: '广东省广州市从化区',
          phone: '15876534944',
          email: 'njjtnc@example.com'
        }
      },
      loading: false
    });
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      // 只替换 127.0.0.1:5000 为 192.168.203.56，不影响其他URL
      if (imageUrl.includes('127.0.0.1:5000')) {
        return imageUrl.replace('127.0.0.1:5000', '192.168.203.56');
      }
      // 如果已经是正确的URL格式，直接返回
      return imageUrl;
    }
    
    // 如果是相对路径，添加基础 URL
    // 确保基础 URL 后面有斜杠
    const baseUrl = 'http://192.168.203.56';
    // 确保图片路径以斜杠开头
    if (!imageUrl.startsWith('/')) {
      imageUrl = '/' + imageUrl;
    }
    return baseUrl + imageUrl;
  }
});