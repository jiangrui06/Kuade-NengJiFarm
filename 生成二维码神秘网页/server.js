const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 9113;
const WWW_DIR = path.join(__dirname, '调用后端获得二维码');

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.json': 'application/json; charset=utf-8',
};

const server = http.createServer((req, res) => {
  // CORS headers so the frontend can call APIs
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    return res.end();
  }

  let filePath = path.join(WWW_DIR, req.url === '/' ? 'index.html' : req.url);
  const ext = path.extname(filePath);

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404, { 'Content-Type': 'text/html; charset=utf-8' });
      return res.end('<h1>404 找不到文件</h1>');
    }
    res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
    res.end(data);
  });
});

server.listen(PORT, '0.0.0.0', () => {
  console.log('========================================');
  console.log('  桌台二维码管理系统 已启动');
  console.log('========================================');
  console.log('  本机访问:   http://localhost:' + PORT);
  console.log('  局域网访问: http://192.168.101.11:' + PORT);
  console.log('  其他设备请使用上面的局域网地址');
  console.log('========================================');
  console.log('  按 Ctrl+C 停止服务');
  console.log('========================================');
});
