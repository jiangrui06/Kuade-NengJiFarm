// 库存扣减管理（本地记录购物车中已加但未下单的商品占用的库存）
const STORAGE_KEY = 'stock_deduction_map';

function getMap() {
  try {
    return wx.getStorageSync(STORAGE_KEY) || {};
  } catch (e) {
    return {};
  }
}

function saveMap(map) {
  try {
    wx.setStorageSync(STORAGE_KEY, map);
  } catch (e) {}
}

module.exports = {
  // 获取指定商品已被占用的库存数（遍历所有未释放的订单累计）
  getByGoodsId(goodsId) {
    const map = getMap();
    let total = 0;
    for (const key in map) {
      const entry = map[key];
      if (entry.goodsList && Array.isArray(entry.goodsList)) {
        for (const item of entry.goodsList) {
          if (String(item.id || item.Id || item.goodsId) === String(goodsId)) {
            total += Number(item.quantity || item.Quantity || 0);
          }
        }
      }
    }
    return total;
  },

  // 保存订单占用的库存记录（下单成功时调用）
  save(orderId, goodsList) {
    const map = getMap();
    map[orderId] = { goodsList: goodsList || [], savedAt: Date.now() };
    saveMap(map);
  },

  // 释放订单占用的库存（订单取消/超时自动取消时调用）
  remove(orderId) {
    const map = getMap();
    delete map[orderId];
    saveMap(map);
  }
};
