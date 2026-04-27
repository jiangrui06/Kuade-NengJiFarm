#!/usr/bin/env python3
"""
本地API服务器，用于测试后厨出餐管理系统
"""

import http.server
import socketserver
import json
import os
from datetime import datetime

PORT = 8080

# 模拟订单数据
def get_default_orders():
    return [
        {
            "id": "ORD001",
            "time": "2026-04-27 10:30:00",
            "table": "A1",
            "items": [
                {"name": "宫保鸡丁", "quantity": 1, "status": False, "price": 28},
                {"name": "鱼香肉丝", "quantity": 1, "status": False, "price": 25},
                {"name": "米饭", "quantity": 2, "status": True, "price": 3}
            ],
            "total": 59
        },
        {
            "id": "ORD002",
            "time": "2026-04-27 10:35:00",
            "table": "取餐号123",
            "items": [
                {"name": "糖醋里脊", "quantity": 1, "status": False, "price": 32},
                {"name": "西红柿鸡蛋汤", "quantity": 1, "status": False, "price": 18}
            ],
            "total": 50
        },
        {
            "id": "ORD003",
            "time": "2026-04-27 10:20:00",
            "table": "B1",
            "items": [
                {"name": "麻婆豆腐", "quantity": 1, "status": True, "price": 22},
                {"name": "青椒肉丝", "quantity": 1, "status": True, "price": 26},
                {"name": "米饭", "quantity": 2, "status": True, "price": 3}
            ],
            "total": 53
        }
    ]

# 存储订单数据
# 每次启动时重置为默认数据
orders = get_default_orders()

class APIHandler(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        # 处理API请求
        if self.path.startswith('/api/'):
            self.handle_api_request()
        else:
            # 处理静态文件请求
            super().do_GET()
    
    def do_POST(self):
        # 处理API POST请求
        if self.path.startswith('/api/'):
            self.handle_api_post_request()
        else:
            super().do_POST()
    
    def handle_api_request(self):
        """处理API GET请求"""
        # 声明全局变量
        global orders
        
        # 去除'/api/'前缀并去掉查询参数
        path = self.path[5:].split('?')[0]  # 去掉'/api/'前缀和查询参数
        
        if path == 'orders':
            # 获取所有订单
            self.send_json_response(orders)
        elif path.startswith('orders/'):
            # 获取单个订单
            # 提取订单ID
            order_id_part = path.split('/')[1]
            order = next((o for o in orders if o['id'] == order_id_part), None)
            if order:
                self.send_json_response(order)
            else:
                self.send_error(404, "Order not found")
        elif path == 'revenue':
            # 获取营业额
            # 只计算已完成订单中已出餐的餐品价格
            total_revenue = 0
            for order in orders:
                # 订单完成的条件：所有餐品都已处理（已出餐或已取消）
                if all(item.get('status', False) or item.get('cancelled', False) for item in order['items']):
                    # 只计算已出餐的餐品价格
                    order_revenue = sum(item['price'] * item['quantity'] for item in order['items'] if item.get('status', False))
                    total_revenue += order_revenue
            self.send_json_response({"total": total_revenue})
        elif path == 'reset':
            # 重置数据
            orders = get_default_orders()
            self.send_json_response({"message": "Data reset successfully"})
        else:
            self.send_error(404, "API endpoint not found")
    
    def handle_api_post_request(self):
        """处理API POST请求"""
        # 声明全局变量
        global orders
        
        path = self.path[5:]  # 去掉'/api/'前缀
        
        # 读取请求体
        content_length = int(self.headers['Content-Length'])
        post_data = self.rfile.read(content_length)
        try:
            data = json.loads(post_data)
        except json.JSONDecodeError:
            self.send_error(400, "Invalid JSON data")
            return
        
        if path.startswith('orders/') and '/items/' in path:
            # 标记菜品为已出餐或已取消
            parts = path.split('/')
            order_id = parts[1]
            item_index = int(parts[3])
            
            order = next((o for o in orders if o['id'] == order_id), None)
            if order and 0 <= item_index < len(order['items']):
                if 'status' in data:
                    order['items'][item_index]['status'] = data['status']
                if 'cancelled' in data:
                    order['items'][item_index]['cancelled'] = data['cancelled']
                self.send_json_response(order)
            else:
                self.send_error(404, "Order or item not found")
        else:
            self.send_error(404, "API endpoint not found")
    
    def send_json_response(self, data):
        """发送JSON响应"""
        self.send_response(200)
        self.send_header('Content-type', 'application/json')
        self.send_header('Access-Control-Allow-Origin', '*')  # 允许跨域请求
        self.end_headers()
        self.wfile.write(json.dumps(data).encode('utf-8'))

# 确保api目录存在
os.makedirs('api', exist_ok=True)

# 启动服务器
# 确保在项目根目录运行
if os.path.basename(os.getcwd()) == 'api':
    os.chdir('..')

print(f"当前工作目录: {os.getcwd()}")

with socketserver.TCPServer(("0.0.0.0", PORT), APIHandler) as httpd:
    print(f"API服务器启动在 http://192.168.101.11:{PORT}")
    print("可用API端点:")
    print(f"  GET  http://192.168.101.11:{PORT}/api/orders - 获取所有订单")
    print(f"  GET  http://192.168.101.11:{PORT}/api/orders/{orders[0]['id']} - 获取单个订单")
    print(f"  GET  http://192.168.101.11:{PORT}/api/revenue - 获取营业额")
    print(f"  POST http://192.168.101.11:{PORT}/api/orders/{orders[0]['id']}/items/0 - 标记菜品为已出餐")
    print("前端页面地址:")
    print(f"  http://192.168.101.11:{PORT}/dashboard/index.html")
    print("按 Ctrl+C 停止服务器")
    httpd.serve_forever()
