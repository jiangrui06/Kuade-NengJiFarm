const api = require('../../utils/api'); 
 
 Page({ 
   data: { 
     cartList: [], 
     totalPrice: '0.00', 
     selectedCount: 0, 
     showModal: false,
     selectAll: false
   }, 
 
   onLoad() { 
     this.restoreCart(); 
   }, 
 
   onShow() { 
     this.restoreCart(); 
   }, 
 
   restoreCart() { 
    let cartList = (wx.getStorageSync('cartList') || []).map(item => ({ 
      ...item, 
      checked: !!item.checked, 
      count: Number(item.count || 0) 
    })); 

    // 如果购物车为空，添加测试数据
    if (cartList.length === 0) {
      cartList = [
        {
          id: '1',
          name: '白糯玉米 800g',
          tag: '云南特产',
          price: 19.9,
          image: 'https://img14.360buyimg.com/n0/jfs/t1/200702/1/35633/9416/64db58f6F668894e4/06f4f4a81c2e9714.jpg',
          count: 2,
          checked: false
        },
        {
          id: '2',
          name: '黄花鱼 500g',
          tag: '新鲜海捕',
          price: 39.9,
          image: 'https://img14.360buyimg.com/n0/jfs/t1/199483/35/35694/12345/64db58f6F668894e4/06f4f4a81c2e9714.jpg',
          count: 1,
          checked: false
        }
      ];
      // 缓存测试数据
      wx.setStorageSync('cartList', cartList);
    }

    this.setData({ cartList }); 
    this.calcTotal(); 
  }, 
 
   syncCart(cartList) { 
     const normalizedCartList = cartList 
       .filter(item => Number(item.count || 0) > 0) 
       .map(item => ({ 
         id: String(item.id), 
         name: item.name || '', 
         price: Number(item.price || 0), 
         image: item.image || '', 
         count: Number(item.count || 0), 
         checked: !!item.checked 
       })); 
 
     this.setData({ cartList: normalizedCartList }); 
     this.calcTotal(); 
     wx.setStorageSync('cartList', normalizedCartList); 
 
     // 尝试同步到后端
     api.request({ 
       url: '/api/AppCart/Appcart', 
       method: 'POST', 
       data: { 
         cartList: normalizedCartList 
       } 
     }) 
       .then((data) => { 
         // 确保后端返回的数据有效
         if (data && data.cartList) {
           const nextCartList = (data.cartList || []).map(item => ({ 
             ...item, 
             checked: !!item.checked, 
             count: Number(item.count || 0) 
           })); 
 
           this.setData({ cartList: nextCartList }); 
           this.calcTotal(); 
           wx.setStorageSync('cartList', nextCartList); 
         }
       }) 
       .catch((err) => { 
         console.error('sync cart failed:', err); 
         wx.showToast({ 
           title: '购物车同步失败，已使用本地数据', 
           icon: 'none',
           duration: 2000
         }); 
       }); 
   }, 
 
   handleMinus(e) { 
     const id = String(e.currentTarget.dataset.id); 
     const cartList = this.data.cartList.map(item => ({ ...item })); 
     const itemIndex = cartList.findIndex(item => String(item.id) === id); 
 
     if (itemIndex === -1) { 
       return; 
     } 
 
     cartList[itemIndex].count -= 1; 
     if (cartList[itemIndex].count <= 0) { 
       cartList.splice(itemIndex, 1); 
     } 
 
     this.syncCart(cartList); 
   }, 
 
   handlePlus(e) { 
     const id = String(e.currentTarget.dataset.id); 
     const cartList = this.data.cartList.map(item => ({ ...item })); 
     const itemIndex = cartList.findIndex(item => String(item.id) === id); 
 
     if (itemIndex === -1) { 
       return; 
     } 
 
     cartList[itemIndex].count += 1; 
     this.syncCart(cartList); 
   }, 
 
   toggleSelect(e) { 
     const id = String(e.currentTarget.dataset.id); 
     const cartList = this.data.cartList.map(item => ({ ...item })); 
     const itemIndex = cartList.findIndex(item => String(item.id) === id); 
 
     if (itemIndex === -1) { 
       return; 
     } 
 
     cartList[itemIndex].checked = !cartList[itemIndex].checked; 
     this.syncCart(cartList); 
   }, 
 
   calcTotal() { 
     let totalPrice = 0; 
     let selectedCount = 0; 
 
     this.data.cartList.forEach((item) => { 
       if (item.checked) { 
         totalPrice += Number(item.price || 0) * Number(item.count || 0); 
         selectedCount += Number(item.count || 0); 
       } 
     }); 
 
     const selectAll = this.data.cartList.length > 0 && this.data.cartList.every(item => item.checked); 
 
     this.setData({ 
       totalPrice: totalPrice.toFixed(2), 
       selectedCount,
       selectAll
     }); 
 
     if (selectedCount > 0) { 
       wx.setTabBarBadge({ 
         index: 2, 
         text: selectedCount.toString() 
       }); 
     } else { 
       wx.removeTabBarBadge({ 
         index: 2 
       }); 
     } 
   }, 
 
   handleSettle() { 
     if (this.data.selectedCount === 0) { 
       wx.showToast({ 
         title: '请先选择商品', 
         icon: 'none' 
       }); 
       return; 
     } 
 
     this.setData({ showModal: true }); 
   }, 
 
   handleConfirmPurchase() { 
     this.setData({ showModal: false }, () => { 
       wx.navigateTo({ 
         url: '../buy/buy' 
       }); 
     }); 
   }, 
 
   handleCancelModal() { 
     this.setData({ showModal: false }); 
   }, 
 
   navTo(e) { 
     const pageMap = { 
       home: '/pages/index/index', 
       activity: '/pages/activity/activity', 
       cart: '/pages/cart/cart', 
       mine: '/pages/profile/profile' 
     }; 
 
     wx.switchTab({ 
       url: pageMap[e.currentTarget.dataset.page] 
     }); 
   },

   handleSelectAll() {
     const selectAll = !this.data.selectAll;
     const cartList = this.data.cartList.map(item => ({
       ...item,
       checked: selectAll
     }));
     // 直接更新本地数据，不调用syncCart避免两次更新
     this.setData({ cartList });
     this.calcTotal();
     wx.setStorageSync('cartList', cartList);
   },

   handleClearCart() {
     wx.showModal({
       title: '确认清空',
       content: '确定要清空购物车吗？',
       success: (res) => {
         if (res.confirm) {
           this.setData({ 
             cartList: [],
             selectAll: false
           });
           this.calcTotal();
           wx.removeStorageSync('cartList');
           wx.showToast({ title: '购物车已清空', icon: 'success' });
         }
       }
     });
   } 
 });