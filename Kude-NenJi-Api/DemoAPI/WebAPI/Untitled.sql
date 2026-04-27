CREATE TABLE `category`  (
  `id` int NOT NULL AUTO_INCREMENT COMMENT '分类id',
  `category_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '分类名称',
  `category_description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '分类描述',
  `category_status` int NULL DEFAULT 1 COMMENT '分类状态',
  `sort_order` int NULL DEFAULT 0 COMMENT '排序号',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品分类表' ROW_FORMAT = DYNAMIC;

CREATE TABLE `commodity`  (
  `commodity_id` int NOT NULL AUTO_INCREMENT COMMENT '商品id',
  `spec_description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '规格描述',
  `in_stock` int NULL DEFAULT 0 COMMENT '库存',
  `quantity` int NULL DEFAULT 0 COMMENT '商品对应的单量',
  `product_status` int NULL DEFAULT 1 COMMENT '商品状态',
  `product_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '商品名称',
  `category_id` int NOT NULL COMMENT '分类id(FK)',
  `image_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '图片url',
  PRIMARY KEY (`commodity_id`) USING BTREE,
  INDEX `idx_category_id`(`category_id` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品表' ROW_FORMAT = DYNAMIC;

CREATE TABLE `commodity_image`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `commodity_id` int NULL DEFAULT NULL,
  `url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `sort_order` int NULL DEFAULT NULL,
  `image_type` int NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `commodity_tag_relation`  (
  `commodity_tag_relation_id` int NOT NULL AUTO_INCREMENT COMMENT '商品关联表id',
  `commodity_id` int NOT NULL COMMENT '商品id',
  `tag_id` int NOT NULL COMMENT '标签id',
  PRIMARY KEY (`commodity_tag_relation_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `dish`  (
  `dish_id` int NOT NULL AUTO_INCREMENT COMMENT '菜品表id',
  `dish_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '菜品名称',
  `dish_description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '菜品描述',
  `dish_price` decimal(10, 2) NOT NULL COMMENT '价格',
  `dish_category_id` int NOT NULL COMMENT '菜品分类id',
  `image_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '图片地址',
  `attribute_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '属性名称',
  `status` int NOT NULL COMMENT '上架状态',
  `limited_edition` int NOT NULL COMMENT '每日限量',
  `dish_sold` int NOT NULL COMMENT '已售卖数量',
  `dish_remaining_quantity` int NOT NULL COMMENT '菜品剩余数量',
  `user_purchase_limit` int NOT NULL COMMENT '用户 限购份数',
  PRIMARY KEY (`dish_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `dish_category`  (
  `dish_category_id` int NOT NULL AUTO_INCREMENT COMMENT '菜单分类id',
  `dish_category_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '分类名称',
  `dish_category_description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '分类描述',
  `dish_category_status` int NOT NULL COMMENT '分类状态',
  `dish_sort_order` int NOT NULL COMMENT '排序号',
  PRIMARY KEY (`dish_category_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `meals_order_tetails`  (
  `meals_order_details_id` int NOT NULL COMMENT '订餐明细id',
  `order_food_id` int NOT NULL COMMENT '订餐表ID(FK)',
  `dish_id` int NOT NULL COMMENT '菜品id(FK)',
  `dish_name` varchar(255) NOT NULL COMMENT '菜品名称',
  `meal_unit_price` decimal(10, 2) NOT NULL COMMENT '单价',
  `meal_order_quantity` int NOT NULL COMMENT '数量',
  `meal_subtotal_amount` decimal(10, 2) NOT NULL COMMENT '小计金额',
  `taste` varchar(255) NOT NULL COMMENT '口味',
  `meal_status` int NOT NULL COMMENT '出餐状态',
  PRIMARY KEY (`meals_order_details_id`)
);

CREATE TABLE `order`  (
  `order_id` bigint NOT NULL AUTO_INCREMENT COMMENT '订单主表id',
  `order_number` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '订单号',
  `user_id` int NOT NULL COMMENT '用户ID',
  `actual_payment` decimal(10, 2) NOT NULL COMMENT '实付',
  `order_type` int NOT NULL COMMENT '订单类型',
  `total_order_amount` decimal(10, 2) NOT NULL COMMENT '订单总金额',
  `order_status` int NOT NULL COMMENT '订单状态',
  `payment_status` int NOT NULL COMMENT '支付状态',
  `delivery_methods` int NOT NULL COMMENT '配送方式',
  `shipping_address` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '收货地址',
  `address_id` int NOT NULL COMMENT '收货地址id(FK)',
  `contact_person` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '联系人',
  `contact_number` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '联系电话',
  `order_creation_time` datetime NOT NULL COMMENT '订单创建时间',
  `payment_time` datetime NOT NULL COMMENT '支付时间',
  `payment_methods` int NOT NULL COMMENT '支付方式',
  PRIMARY KEY (`order_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `order_details`  (
  `order_details_id` bigint NOT NULL AUTO_INCREMENT COMMENT '明细id',
  `order_id` bigint NOT NULL COMMENT '订单ID',
  `commodity_id` int NOT NULL COMMENT '商品ID',
  `actual_unit_price` decimal(10, 2) NOT NULL COMMENT '实际成交价',
  `unit_price` decimal(10, 2) NOT NULL COMMENT '单价',
  `purchase_quantity` int NOT NULL COMMENT '购买数量',
  `subtotal_amount` decimal(10, 2) NOT NULL COMMENT '小计金额',
  PRIMARY KEY (`order_details_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `order_food`  (
  `order_food_id` int NOT NULL COMMENT '订餐表id',
  `order_id` bigint NOT NULL COMMENT '订单主表id',
  `menu_number` varchar(50) NOT NULL COMMENT '菜单号',
  `table_number` int NOT NULL COMMENT '取餐号',
  `number_of_diners` int NOT NULL COMMENT '就餐人数',
  `remark` varchar(255) NOT NULL COMMENT '备注',
  `creation_time` datetime NOT NULL COMMENT '创建时间',
  `meal_serving_time` datetime NOT NULL COMMENT '出餐时间',
  `order_status` int NOT NULL COMMENT '状态',
  `user_id` int NOT NULL COMMENT '用户id',
  PRIMARY KEY (`order_food_id`)
);

CREATE TABLE `role`  (
  `role_id` int NOT NULL AUTO_INCREMENT COMMENT '角色ID',
  `role_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '角色名称',
  PRIMARY KEY (`role_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `shipping_address`  (
  `address_id` int NOT NULL AUTO_INCREMENT COMMENT '收货地址id',
  `user_id` int NOT NULL COMMENT '用户id(FK)',
  `contact_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '用户姓名',
  `province` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '省份',
  `city` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '市',
  `municipal_district` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '区/县',
  `town` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '镇/街道',
  `house_number` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '门牌号',
  PRIMARY KEY (`address_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `shipping_cart`  (
  `shipping_cart_id` int NOT NULL AUTO_INCREMENT COMMENT '购物车表id',
  `user_id` int NOT NULL COMMENT '用户表id(FK)',
  `cart_quantity` int NOT NULL COMMENT '购买数量',
  `commodity_id` int NOT NULL COMMENT '商品表id',
  `join_time` datetime NOT NULL COMMENT '加入购物车时间',
  PRIMARY KEY (`shipping_cart_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `tag`  (
  `tag_id` int NOT NULL AUTO_INCREMENT COMMENT '标签id',
  `tag_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '标签名称',
  PRIMARY KEY (`tag_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

CREATE TABLE `user`  (
  `user_id` int NOT NULL AUTO_INCREMENT COMMENT '用户id',
  `user_no` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '用户名',
  `phone_number` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '手机号',
  `register_time` datetime NOT NULL COMMENT '注册时间',
  `wx_openid` varchar(255) NOT NULL COMMENT '微信id',
  `wx_image` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '微信头像',
  `wx_name` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '微信名称',
  `role_id` int NOT NULL COMMENT '角色id',
  PRIMARY KEY (`user_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

ALTER TABLE `commodity` ADD CONSTRAINT `fk_commodity_category` FOREIGN KEY (`category_id`) REFERENCES `category` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `commodity_image` ADD CONSTRAINT `fk_commodity_image_commodity_1` FOREIGN KEY (`commodity_id`) REFERENCES `commodity` (`commodity_id`) ON DELETE CASCADE;
ALTER TABLE `commodity_tag_relation` ADD CONSTRAINT `fk_commodity_tag_relation_commodity_1` FOREIGN KEY (`commodity_id`) REFERENCES `commodity` (`commodity_id`) ON DELETE CASCADE;
ALTER TABLE `commodity_tag_relation` ADD CONSTRAINT `fk_commodity_tag_relation_tag_2` FOREIGN KEY (`tag_id`) REFERENCES `tag` (`tag_id`) ON DELETE CASCADE;
ALTER TABLE `dish` ADD CONSTRAINT `fk_dish_dish_category_1` FOREIGN KEY (`dish_category_id`) REFERENCES `dish_category` (`dish_category_id`);
ALTER TABLE `meals_order_tetails` ADD CONSTRAINT `fk_meals_order_tetails_dish_1` FOREIGN KEY (`dish_id`) REFERENCES `dish` (`dish_id`);
ALTER TABLE `meals_order_tetails` ADD CONSTRAINT `fk_meals_order_tetails_order__food_2` FOREIGN KEY (`order_food_id`) REFERENCES `order_food` (`order_food_id`);
ALTER TABLE `order` ADD CONSTRAINT `fk_order_user_1` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`);
ALTER TABLE `order` ADD CONSTRAINT `fk_order_shipping_address_3` FOREIGN KEY (`address_id`) REFERENCES `shipping_address` (`address_id`);
ALTER TABLE `order_details` ADD CONSTRAINT `fk_order_details_commodity_2` FOREIGN KEY (`commodity_id`) REFERENCES `commodity` (`commodity_id`);
ALTER TABLE `order_details` ADD CONSTRAINT `fk_order_details_order_2` FOREIGN KEY (`order_id`) REFERENCES `order` (`order_id`);
ALTER TABLE `order_food` ADD CONSTRAINT `fk_order__food_user_1` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`);
ALTER TABLE `order_food` ADD CONSTRAINT `fk_order__food_order_2` FOREIGN KEY (`order_id`) REFERENCES `order` (`order_id`);
ALTER TABLE `shipping_address` ADD CONSTRAINT `fk_shipping_address_user_1` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`);
ALTER TABLE `shipping_cart` ADD CONSTRAINT `fk_shipping_cart_user_1` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`);
ALTER TABLE `shipping_cart` ADD CONSTRAINT `fk_shipping_cart_commodity_2` FOREIGN KEY (`commodity_id`) REFERENCES `commodity` (`commodity_id`);
ALTER TABLE `user` ADD CONSTRAINT `fk_user_role_3` FOREIGN KEY (`role_id`) REFERENCES `role` (`role_id`);

