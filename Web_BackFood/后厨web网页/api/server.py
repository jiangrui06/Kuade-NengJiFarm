#!/usr/bin/env python3
"""
后厨系统 Mock API 服务器
Base URL: /api/Kitchen
"""

import http.server
import socketserver
import json
import os
from datetime import datetime, timedelta

PORT = 8080

# Mock 数据存储
users = {
    "13800138000": {"password": "123456", "user_id": 1, "user_name": "张厨师", "role_id": 4},
    "admin": {"password": "admin", "user_id": 2, "user_name": "李厨师", "role_id": 4},
}

orders = [
    {
        "id": 1001,
        "no": "ORD20260507001",
        "time": (datetime.now() - timedelta(hours=2)).isoformat(),
        "table": "A1",
        "total": 128.00,
        "items": [
            {"name": "宫保鸡丁", "quantity": 1, "status": 2, "price": 38},
            {"name": "鱼香肉丝", "quantity": 1, "status": 0, "price": 32},
            {"name": "米饭", "quantity": 2, "status": 0, "price": 3},
        ]
    },
    {
        "id": 1002,
        "no": "ORD20260507002",
        "time": (datetime.now() - timedelta(hours=1)).isoformat(),
        "table": "B3",
        "total": 256.00,
        "items": [
            {"name": "糖醋排骨", "quantity": 1, "status": 0, "price": 68},
            {"name": "麻婆豆腐", "quantity": 1, "status": 0, "price": 28},
            {"name": "西湖牛肉羹", "quantity": 1, "status": 0, "price": 48},
        ]
    },
    {
        "id": 1003,
        "no": "ORD20260507003",
        "time": (datetime.now() - timedelta(minutes=30)).isoformat(),
        "table": "C2",
        "total": 89.00,
        "items": [
            {"name": "番茄炒蛋", "quantity": 1, "status": 2, "price": 22},
            {"name": "紫菜蛋花汤", "quantity": 1, "status": 2, "price": 18},
            {"name": "米饭", "quantity": 2, "status": 2, "price": 3},
        ]
    }
]

class APIHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, format, *args):
        # 简化日志输出
        print(f"[{datetime.now().strftime('%H:%M:%S')}] {args[0]}")

    def send_json(self, code, msg, data=None):
        self.send_response(200)
        self.send_header('Content-type', 'application/json')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type, Authorization')
        self.end_headers()
        self.wfile.write(json.dumps({"code": code, "msg": msg, "data": data}, ensure_ascii=False).encode())

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type, Authorization')
        self.end_headers()

    def do_GET(self):
        path = self.path
        
        # API 路由
        if path.startswith('/api/Kitchen/'):
            self.handle_api_get(path[13:])  # 去掉 /api/Kitchen/
        else:
            # 静态文件
            super().do_GET()

    def do_POST(self):
        path = self.path
        
        if path.startswith('/api/Kitchen/'):
            self.handle_api_post(path[13:])
        else:
            self.send_error(404)

    def handle_api_get(self, path):
        """处理 API GET 请求"""
        global orders
        
        if path.startswith('order/list'):
            # 获取订单列表 ?type=0|1
            import urllib.parse
            parsed = urllib.parse.urlparse(path)
            params = urllib.parse.parse_qs(parsed.query)
            order_type = int(params.get('type', [0])[0])
            
            # type=0: 待出餐(有未出餐菜品), type=1: 已出餐(全部已出餐)
            if order_type == 0:
                result = [o for o in orders if any(it['status'] != 2 for it in o['items'])]
            else:
                result = [o for o in orders if all(it['status'] == 2 for it in o['items'])]
            
            self.send_json(0, "success", result)
            
        elif path.startswith('order/detail'):
            # 获取订单详情 ?orderId=xxx
            import urllib.parse
            parsed = urllib.parse.urlparse(path)
            params = urllib.parse.parse_qs(parsed.query)
            order_id = int(params.get('orderId', [0])[0])
            
            order = next((o for o in orders if o['id'] == order_id), None)
            if order:
                # 构造详细响应
                detail = {
                    "orderId": order['id'],
                    "orderNo": order['no'],
                    "tableNumber": order['table'],
                    "createTime": order['time'],
                    "totalAmount": order['total'],
                    "dishList": [
                        {
                            "id": idx + 1000,  # dishOrderDetailsId
                            "name": it['name'],
                            "quantity": it['quantity'],
                            "status": it['status'],
                            "price": it['price']
                        } for idx, it in enumerate(order['items'])
                    ]
                }
                self.send_json(0, "success", detail)
            else:
                self.send_json(-1, "订单不存在", None)
                
        elif path == 'today-statistics':
            # 今日统计
            total_amount = sum(o['total'] for o in orders)
            total_order = len(orders)
            finished_order = len([o for o in orders if all(it['status'] == 2 for it in o['items'])])
            pending_dish = sum(len([it for it in o['items'] if it['status'] != 2]) for o in orders)
            finished_dish = sum(len([it for it in o['items'] if it['status'] == 2]) for o in orders)
            
            self.send_json(0, "success", {
                "todayTotalAmount": total_amount,
                "todayTotalOrder": total_order,
                "todayFinishedOrder": finished_order,
                "todayPendingDish": pending_dish,
                "todayFinishedDish": finished_dish
            })
        else:
            self.send_json(-1, "接口不存在", None)

    def handle_api_post(self, path):
        """处理 API POST 请求"""
        global orders
        
        # 读取请求体
        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length).decode('utf-8')
        try:
            data = json.loads(post_data) if post_data else {}
        except:
            data = {}
        
        if path == 'login':
            # 登录
            phone = data.get('phoneNumber', '')
            pwd = data.get('password', '')
            
            user = users.get(phone)
            if user and user['password'] == pwd and user['role_id'] == 4:
                self.send_json(0, "登录成功", {
                    "token": f"mock_token_{phone}_{datetime.now().timestamp()}",
                    "user_id": user['user_id'],
                    "user_name": user['user_name'],
                    "phone_number": phone
                })
            else:
                self.send_json(-1, "账号或密码错误，仅后厨人员可登录", None)
                
        elif path == 'logout':
            self.send_json(0, "登出成功", None)
            
        elif path == 'dish/finish':
            # 标记菜品出餐
            dish_id = data.get('dishOrderDetailsId')
            
            # 查找并更新菜品状态
            for order in orders:
                for idx, item in enumerate(order['items']):
                    if idx + 1000 == dish_id:
                        item['status'] = 2
                        # 计算完成进度
                        total = len(order['items'])
                        finished = len([it for it in order['items'] if it['status'] == 2])
                        self.send_json(0, "标记成功", {
                            "allFinished": finished == total,
                            "finishDish": finished,
                            "totalDish": total
                        })
                        return
            
            self.send_json(-1, "菜品不存在", None)
        else:
            self.send_json(-1, "接口不存在", None)

if __name__ == '__main__':
    os.chdir(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    
    with socketserver.TCPServer(("0.0.0.0", PORT), APIHandler) as httpd:
        print(f"\n🚀 后厨 Mock API 服务器启动")
        print(f"📍 地址: http://localhost:{PORT}")
        print(f"📄 登录页: http://localhost:{PORT}/login/index.html")
        print(f"\n测试账号:")
        print(f"  手机号: 13800138000, 密码: 123456")
        print(f"  手机号: admin, 密码: admin")
        print(f"\n按 Ctrl+C 停止服务器\n")
        httpd.serve_forever()
