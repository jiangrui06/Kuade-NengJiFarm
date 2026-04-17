// 统一入口文件

// 导入 API 封装
const api = require('./api');

// 导入工具函数
const utils = require('./utils');

// 导出所有功能
module.exports = {
  // API 相关
  request: api.request,
  get: api.get,
  post: api.post,
  put: api.put,
  del: api.del,
  upload: api.upload,
  api: api.api,
  
  // 工具函数相关
  storage: utils.storage,
  time: utils.time,
  validate: utils.validate,
  number: utils.number,
  string: utils.string,
  array: utils.array,
  navigation: utils.navigation,
  toast: utils.toast
};