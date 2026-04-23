/*
 Navicat Premium Dump SQL

 Source Server         : MySql1
 Source Server Type    : MySQL
 Source Server Version : 80036 (8.0.36)
 Source Host           : localhost:3306
 Source Schema         : demo_4

 Target Server Type    : MySQL
 Target Server Version : 80036 (8.0.36)
 File Encoding         : 65001

 Date: 23/04/2026 16:38:05
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for acre_material
-- ----------------------------
DROP TABLE IF EXISTS `acre_material`;
CREATE TABLE `acre_material`  (
  `material_id` bigint NOT NULL AUTO_INCREMENT COMMENT '素材ID',
  `acre_project_id` bigint NOT NULL COMMENT '认养项目ID',
  `material_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '素材类型 0图 1详情图 2视频',
  `material_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '素材地址',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `sort_order` int NOT NULL DEFAULT 0 COMMENT '排序',
  PRIMARY KEY (`material_id`) USING BTREE,
  INDEX `idx_acre_material_project`(`acre_project_id` ASC, `sort_order` ASC) USING BTREE,
  CONSTRAINT `fk_acre_material_acre_project` FOREIGN KEY (`acre_project_id`) REFERENCES `acre_project` (`acre_project_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '认养项目素材表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for acre_order_detail
-- ----------------------------
DROP TABLE IF EXISTS `acre_order_detail`;
CREATE TABLE `acre_order_detail`  (
  `acre_order_details_id` bigint NOT NULL AUTO_INCREMENT COMMENT '认养订单明细ID',
  `order_id` bigint NOT NULL COMMENT '订单ID',
  `acre_proiect_id` bigint NOT NULL COMMENT '认养项目ID',
  `unit_price` decimal(10, 2) NOT NULL COMMENT '单价',
  `purchase_quantity` int NOT NULL COMMENT '购买数量',
  `subtotal_amount` decimal(10, 2) NOT NULL COMMENT '小计金额',
  `acre_qrcode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '核销码URL',
  PRIMARY KEY (`acre_order_details_id`) USING BTREE,
  INDEX `idx_acre_order_detail_order_id`(`order_id` ASC) USING BTREE,
  INDEX `idx_acre_order_detail_project_id`(`acre_proiect_id` ASC) USING BTREE,
  CONSTRAINT `fk_acre_order_detail_order` FOREIGN KEY (`order_id`) REFERENCES `acre_project_orders` (`order_id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_acre_order_detail_project` FOREIGN KEY (`acre_proiect_id`) REFERENCES `acre_project` (`acre_project_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '认养订单明细表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for acre_order_status
-- ----------------------------
DROP TABLE IF EXISTS `acre_order_status`;
CREATE TABLE `acre_order_status`  (
  `acre_order_status_id` int NOT NULL AUTO_INCREMENT COMMENT '认养订单状态ID',
  `status_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`acre_order_status_id`) USING BTREE,
  UNIQUE INDEX `uk_acre_order_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '认养订单状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for acre_project
-- ----------------------------
DROP TABLE IF EXISTS `acre_project`;
CREATE TABLE `acre_project`  (
  `acre_project_id` bigint NOT NULL AUTO_INCREMENT COMMENT '认养项目ID',
  `name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '农场项目名称',
  `description` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '项目描述',
  `total_acres` decimal(10, 2) NOT NULL DEFAULT 1.00 COMMENT '总亩数',
  `crop_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '农作物名称',
  `price_per_acre` decimal(10, 2) NULL DEFAULT NULL COMMENT '每亩价格',
  `status_id` int NULL DEFAULT NULL COMMENT '认养项目状态ID',
  `image_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '封面图',
  `sort_order` int NOT NULL DEFAULT 0 COMMENT '排序',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`acre_project_id`) USING BTREE,
  INDEX `idx_acre_project_sort`(`sort_order` ASC) USING BTREE,
  INDEX `idx_acre_project_status_id`(`status_id` ASC) USING BTREE,
  CONSTRAINT `fk_acre_project_status_dict` FOREIGN KEY (`status_id`) REFERENCES `acre_project_status_dict` (`acre_project_status_id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '认养项目表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for acre_project_orders
-- ----------------------------
DROP TABLE IF EXISTS `acre_project_orders`;
CREATE TABLE `acre_project_orders`  (
  `order_id` bigint NOT NULL AUTO_INCREMENT COMMENT '认购订单ID',
  `order_no` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '平台订单号',
  `wx_pay_no` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '微信支付订单号',
  `total_amount` decimal(10, 2) NOT NULL COMMENT '订单总金额',
  `total_quantity` int NOT NULL COMMENT '订单项目总数',
  `order_status` int NOT NULL COMMENT '订单状态ID',
  `user_id` int NOT NULL COMMENT '下单人ID',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '下单时间',
  `acre_order_status_id` int NOT NULL COMMENT '认购状态ID',
  PRIMARY KEY (`order_id`) USING BTREE,
  UNIQUE INDEX `uk_acre_project_orders_order_no`(`order_no` ASC) USING BTREE,
  INDEX `idx_acre_project_orders_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_acre_project_orders_order_status`(`order_status` ASC) USING BTREE,
  INDEX `idx_acre_project_orders_acre_order_status_id`(`acre_order_status_id` ASC) USING BTREE,
  INDEX `idx_acre_project_orders_create_time`(`create_time` ASC) USING BTREE,
  CONSTRAINT `fk_acre_project_orders_acre_order_status` FOREIGN KEY (`acre_order_status_id`) REFERENCES `acre_order_status` (`acre_order_status_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_acre_project_orders_status` FOREIGN KEY (`order_status`) REFERENCES `acre_order_status` (`acre_order_status_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_acre_project_orders_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '认购一亩田订单表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for acre_project_status_dict
-- ----------------------------
DROP TABLE IF EXISTS `acre_project_status_dict`;
CREATE TABLE `acre_project_status_dict`  (
  `acre_project_status_id` int NOT NULL AUTO_INCREMENT COMMENT '认养项目状态ID',
  `status_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`acre_project_status_id`) USING BTREE,
  UNIQUE INDEX `uk_acre_project_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '认养项目状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for acre_verification_record
-- ----------------------------
DROP TABLE IF EXISTS `acre_verification_record`;
CREATE TABLE `acre_verification_record`  (
  `record_id` bigint NOT NULL AUTO_INCREMENT COMMENT '核销记录ID',
  `acre_order_details_id` bigint NOT NULL COMMENT '认养订单明细ID',
  `verification_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '核销时间',
  PRIMARY KEY (`record_id`) USING BTREE,
  INDEX `idx_acre_verification_detail_id`(`acre_order_details_id` ASC) USING BTREE,
  INDEX `idx_acre_verification_time`(`verification_time` ASC) USING BTREE,
  CONSTRAINT `fk_acre_verification_record_detail` FOREIGN KEY (`acre_order_details_id`) REFERENCES `acre_order_detail` (`acre_order_details_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '认养核销记录表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for activity
-- ----------------------------
DROP TABLE IF EXISTS `activity`;
CREATE TABLE `activity`  (
  `activity_id` bigint NOT NULL AUTO_INCREMENT COMMENT '活动ID',
  `title` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '活动标题',
  `price` decimal(10, 2) NOT NULL COMMENT '价格',
  `start_date` datetime NOT NULL COMMENT '开始日期',
  `end_date` datetime NOT NULL COMMENT '结束日期',
  `image_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '活动图片',
  `description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '' COMMENT '活动简介',
  `location` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '' COMMENT '活动地点',
  `people` int NOT NULL DEFAULT 0 COMMENT '活动报名上限人数',
  `content` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL COMMENT '活动内容',
  `status_id` int NOT NULL DEFAULT 1 COMMENT '活动状态ID',
  `sort_order` int NOT NULL DEFAULT 0 COMMENT '排序',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`activity_id`) USING BTREE,
  INDEX `idx_activity_sort`(`sort_order` ASC) USING BTREE,
  INDEX `idx_activity_status_id`(`status_id` ASC) USING BTREE,
  INDEX `idx_activity_date`(`start_date` ASC, `end_date` ASC) USING BTREE,
  CONSTRAINT `fk_activity_status` FOREIGN KEY (`status_id`) REFERENCES `activity_status` (`activity_status_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '活动表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for activity_material
-- ----------------------------
DROP TABLE IF EXISTS `activity_material`;
CREATE TABLE `activity_material`  (
  `activity_material_id` bigint NOT NULL AUTO_INCREMENT COMMENT '素材ID',
  `activity_id` bigint NOT NULL COMMENT '活动ID',
  `material_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '素材类型 0图 1详情图 2视频',
  `material_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '素材地址',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `sort_order` int NOT NULL DEFAULT 0 COMMENT '排序',
  PRIMARY KEY (`activity_material_id`) USING BTREE,
  INDEX `idx_activity_material_activity`(`activity_id` ASC, `sort_order` ASC) USING BTREE,
  CONSTRAINT `fk_activity_material_activity` FOREIGN KEY (`activity_id`) REFERENCES `activity` (`activity_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '活动素材表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for activity_order_detail
-- ----------------------------
DROP TABLE IF EXISTS `activity_order_detail`;
CREATE TABLE `activity_order_detail`  (
  `activity_order_details_id` bigint NOT NULL AUTO_INCREMENT COMMENT '活动订单明细ID',
  `activity_order_id` bigint NOT NULL COMMENT '活动订单ID',
  `activity_id` bigint NOT NULL COMMENT '活动ID',
  `unit_price` decimal(10, 2) NOT NULL COMMENT '单价',
  `quantity` int NOT NULL COMMENT '参与人数',
  `subtotal_amount` decimal(10, 2) NOT NULL COMMENT '小计金额',
  `activity_qrcode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '核销码URL',
  PRIMARY KEY (`activity_order_details_id`) USING BTREE,
  INDEX `idx_activity_order_detail_order_id`(`activity_order_id` ASC) USING BTREE,
  INDEX `idx_activity_order_detail_activity_id`(`activity_id` ASC) USING BTREE,
  CONSTRAINT `fk_activity_order_detail_activity` FOREIGN KEY (`activity_id`) REFERENCES `activity` (`activity_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_activity_order_detail_order` FOREIGN KEY (`activity_order_id`) REFERENCES `activity_orders` (`order_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '活动订单明细表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for activity_order_status
-- ----------------------------
DROP TABLE IF EXISTS `activity_order_status`;
CREATE TABLE `activity_order_status`  (
  `activity_order_status_id` int NOT NULL AUTO_INCREMENT COMMENT '活动订单状态ID',
  `status_name` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`activity_order_status_id`) USING BTREE,
  UNIQUE INDEX `uk_activity_order_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '活动订单状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for activity_orders
-- ----------------------------
DROP TABLE IF EXISTS `activity_orders`;
CREATE TABLE `activity_orders`  (
  `order_id` bigint NOT NULL AUTO_INCREMENT COMMENT '活动订单ID',
  `order_no` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '平台订单号',
  `wx_pay_no` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '微信支付订单号',
  `total_amount` decimal(10, 2) NOT NULL COMMENT '订单总金额',
  `total_quantity` int NOT NULL COMMENT '订单总人数',
  `order_status_id` int NOT NULL COMMENT '订单状态ID',
  `user_id` int NOT NULL COMMENT '下单人ID',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '下单时间',
  PRIMARY KEY (`order_id`) USING BTREE,
  UNIQUE INDEX `uk_activity_orders_order_no`(`order_no` ASC) USING BTREE,
  INDEX `idx_activity_orders_status_id`(`order_status_id` ASC) USING BTREE,
  INDEX `idx_activity_orders_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_activity_orders_create_time`(`create_time` ASC) USING BTREE,
  CONSTRAINT `fk_activity_orders_status` FOREIGN KEY (`order_status_id`) REFERENCES `activity_order_status` (`activity_order_status_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_activity_orders_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '活动订单表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for activity_status
-- ----------------------------
DROP TABLE IF EXISTS `activity_status`;
CREATE TABLE `activity_status`  (
  `activity_status_id` int NOT NULL AUTO_INCREMENT COMMENT '活动状态ID',
  `status_name` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`activity_status_id`) USING BTREE,
  UNIQUE INDEX `uk_activity_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '活动状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for activity_verification_record
-- ----------------------------
DROP TABLE IF EXISTS `activity_verification_record`;
CREATE TABLE `activity_verification_record`  (
  `record_id` bigint NOT NULL AUTO_INCREMENT COMMENT '核销记录ID',
  `activity_order_details_id` bigint NOT NULL COMMENT '活动订单明细ID',
  `verification_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '核销时间',
  PRIMARY KEY (`record_id`) USING BTREE,
  INDEX `idx_activity_verification_detail_id`(`activity_order_details_id` ASC) USING BTREE,
  INDEX `idx_activity_verification_time`(`verification_time` ASC) USING BTREE,
  CONSTRAINT `fk_activity_verification_record_detail` FOREIGN KEY (`activity_order_details_id`) REFERENCES `activity_order_detail` (`activity_order_details_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '活动核销记录表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for admin
-- ----------------------------
DROP TABLE IF EXISTS `admin`;
CREATE TABLE `admin`  (
  `admin_id` int NOT NULL AUTO_INCREMENT COMMENT '管理员ID',
  `user_no` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '管理员账号',
  `user_password` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '密码',
  PRIMARY KEY (`admin_id`) USING BTREE,
  UNIQUE INDEX `uk_admin_user_no`(`user_no` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '管理员表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for admin_operation_log
-- ----------------------------
DROP TABLE IF EXISTS `admin_operation_log`;
CREATE TABLE `admin_operation_log`  (
  `log_id` int NOT NULL AUTO_INCREMENT COMMENT '管理员操作日志ID',
  `user_id` int NOT NULL COMMENT '操作人ID',
  `operation_type` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '操作类型',
  `target_table` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '操作表名',
  `target_id` int NOT NULL COMMENT '操作对象ID',
  `operation_content` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '操作内容',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`log_id`) USING BTREE,
  INDEX `idx_admin_operation_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_admin_operation_create_time`(`create_time` ASC) USING BTREE,
  CONSTRAINT `fk_admin_operation_log_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '管理员操作日志表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for carousel
-- ----------------------------
DROP TABLE IF EXISTS `carousel`;
CREATE TABLE `carousel`  (
  `carousel_id` bigint NOT NULL AUTO_INCREMENT COMMENT '轮播ID',
  `image_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '轮播图',
  `link_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '跳转链接',
  `position` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT 'home' COMMENT '位置 home/goods/acres',
  `sort_order` int NOT NULL DEFAULT 0 COMMENT '排序',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`carousel_id`) USING BTREE,
  INDEX `idx_carousel_pos_sort`(`position` ASC, `sort_order` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '轮播图表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for category
-- ----------------------------
DROP TABLE IF EXISTS `category`;
CREATE TABLE `category`  (
  `id` int NOT NULL AUTO_INCREMENT COMMENT '分类ID',
  `category_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '分类名称',
  `category_description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '分类描述',
  `category_status_id` int NULL DEFAULT NULL COMMENT '分类状态ID',
  `sort_order` int NOT NULL DEFAULT 0 COMMENT '排序号',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_category_name`(`category_name` ASC) USING BTREE,
  INDEX `idx_category_status_id`(`category_status_id` ASC) USING BTREE,
  INDEX `idx_category_sort_order`(`sort_order` ASC) USING BTREE,
  CONSTRAINT `fk_category_status` FOREIGN KEY (`category_status_id`) REFERENCES `category_status` (`category_status_id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品分类表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for category_status
-- ----------------------------
DROP TABLE IF EXISTS `category_status`;
CREATE TABLE `category_status`  (
  `category_status_id` int NOT NULL AUTO_INCREMENT COMMENT '分类状态ID',
  `status_name` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`category_status_id`) USING BTREE,
  UNIQUE INDEX `uk_category_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品分类状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for commodity
-- ----------------------------
DROP TABLE IF EXISTS `commodity`;
CREATE TABLE `commodity`  (
  `commodity_id` int NOT NULL AUTO_INCREMENT COMMENT '商品ID',
  `spec_description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '规格描述',
  `in_stock` int NOT NULL DEFAULT 0 COMMENT '库存',
  `quantity` int NOT NULL DEFAULT 0 COMMENT '商品对应单位数量',
  `product_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '商品名称',
  `category_id` int NOT NULL COMMENT '分类ID',
  `image_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '主图',
  `unit_price` decimal(10, 2) NOT NULL DEFAULT 0.00 COMMENT '售价',
  `weight_text` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '' COMMENT '规格重量文本',
  `storage_condition` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '' COMMENT '储存条件',
  `unit_id` int NULL DEFAULT NULL COMMENT '单位ID',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '修改时间',
  `commodity_status_id` int NULL DEFAULT NULL COMMENT '商品状态ID',
  PRIMARY KEY (`commodity_id`) USING BTREE,
  INDEX `idx_commodity_category_id`(`category_id` ASC) USING BTREE,
  INDEX `idx_commodity_unit_id`(`unit_id` ASC) USING BTREE,
  INDEX `idx_commodity_status_id`(`commodity_status_id` ASC) USING BTREE,
  INDEX `idx_commodity_product_name`(`product_name` ASC) USING BTREE,
  CONSTRAINT `fk_commodity_category` FOREIGN KEY (`category_id`) REFERENCES `category` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_commodity_status` FOREIGN KEY (`commodity_status_id`) REFERENCES `commodity_status` (`commodity_status_id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_commodity_unit` FOREIGN KEY (`unit_id`) REFERENCES `unit` (`unit_id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for commodity_material
-- ----------------------------
DROP TABLE IF EXISTS `commodity_material`;
CREATE TABLE `commodity_material`  (
  `material_id` bigint NOT NULL AUTO_INCREMENT COMMENT '素材ID',
  `commodity_id` int NOT NULL COMMENT '商品ID',
  `material_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '素材类型 0图 1详情图 2视频',
  `material_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '素材地址',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `sort_order` int NULL DEFAULT 0 COMMENT '排序',
  PRIMARY KEY (`material_id`) USING BTREE,
  INDEX `idx_commodity_material_commodity`(`commodity_id` ASC, `sort_order` ASC) USING BTREE,
  CONSTRAINT `fk_commodity_material_commodity` FOREIGN KEY (`commodity_id`) REFERENCES `commodity` (`commodity_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品素材表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for commodity_order_detail
-- ----------------------------
DROP TABLE IF EXISTS `commodity_order_detail`;
CREATE TABLE `commodity_order_detail`  (
  `commodity_order_details_id` bigint NOT NULL AUTO_INCREMENT COMMENT '商品订单明细ID',
  `order_id` bigint NOT NULL COMMENT '订单ID',
  `commodity_id` int NOT NULL COMMENT '商品ID',
  `unit_price` decimal(10, 2) NOT NULL COMMENT '商品单价',
  `quantity` int NOT NULL COMMENT '订购数量',
  `subtotal_amount` decimal(10, 2) NOT NULL COMMENT '小计金额',
  `status_id` int NULL DEFAULT NULL COMMENT '商品状态ID',
  PRIMARY KEY (`commodity_order_details_id`) USING BTREE,
  INDEX `idx_commodity_order_detail_order_id`(`order_id` ASC) USING BTREE,
  INDEX `idx_commodity_order_detail_commodity_id`(`commodity_id` ASC) USING BTREE,
  INDEX `idx_commodity_order_detail_status_id`(`status_id` ASC) USING BTREE,
  CONSTRAINT `fk_commodity_order_detail_commodity` FOREIGN KEY (`commodity_id`) REFERENCES `commodity` (`commodity_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_commodity_order_detail_order` FOREIGN KEY (`order_id`) REFERENCES `commodity_orders` (`order_id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_commodity_order_detail_status` FOREIGN KEY (`status_id`) REFERENCES `commodity_status` (`commodity_status_id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品订单明细表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for commodity_orders
-- ----------------------------
DROP TABLE IF EXISTS `commodity_orders`;
CREATE TABLE `commodity_orders`  (
  `order_id` bigint NOT NULL AUTO_INCREMENT COMMENT '商品订单ID',
  `order_no` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '平台订单号',
  `wx_pay_no` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '微信支付订单号',
  `total_amount` decimal(10, 2) NOT NULL COMMENT '订单总金额',
  `total_quantity` int NOT NULL COMMENT '订单商品总数',
  `order_status` int NOT NULL COMMENT '订单状态ID',
  `user_id` int NOT NULL COMMENT '下单人ID',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '下单时间',
  `address_id` bigint NOT NULL COMMENT '收货地址ID',
  `tracking_number` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '快递单号',
  `tracking_type_id` bigint NULL DEFAULT NULL COMMENT '物流类型ID',
  PRIMARY KEY (`order_id`) USING BTREE,
  UNIQUE INDEX `uk_commodity_orders_order_no`(`order_no` ASC) USING BTREE,
  INDEX `idx_commodity_orders_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_commodity_orders_status`(`order_status` ASC) USING BTREE,
  INDEX `idx_commodity_orders_address_id`(`address_id` ASC) USING BTREE,
  INDEX `idx_commodity_orders_tracking_type_id`(`tracking_type_id` ASC) USING BTREE,
  INDEX `idx_commodity_orders_create_time`(`create_time` ASC) USING BTREE,
  CONSTRAINT `fk_commodity_orders_address` FOREIGN KEY (`address_id`) REFERENCES `shipping_address` (`address_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_commodity_orders_status` FOREIGN KEY (`order_status`) REFERENCES `commodity_status` (`commodity_status_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_commodity_orders_tracking_type` FOREIGN KEY (`tracking_type_id`) REFERENCES `tracking_type` (`tracking_type_id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_commodity_orders_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品订单表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for commodity_status
-- ----------------------------
DROP TABLE IF EXISTS `commodity_status`;
CREATE TABLE `commodity_status`  (
  `commodity_status_id` int NOT NULL AUTO_INCREMENT COMMENT '商品状态ID',
  `status_name` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`commodity_status_id`) USING BTREE,
  UNIQUE INDEX `uk_commodity_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for commodity_tag_detail
-- ----------------------------
DROP TABLE IF EXISTS `commodity_tag_detail`;
CREATE TABLE `commodity_tag_detail`  (
  `commodity_tag_relation_id` int NOT NULL AUTO_INCREMENT COMMENT '商品标签关联ID',
  `commodity_id` int NOT NULL COMMENT '商品ID',
  `tag_id` int NOT NULL COMMENT '标签ID',
  PRIMARY KEY (`commodity_tag_relation_id`) USING BTREE,
  UNIQUE INDEX `uk_commodity_tag`(`commodity_id` ASC, `tag_id` ASC) USING BTREE,
  INDEX `idx_commodity_tag_tag_id`(`tag_id` ASC) USING BTREE,
  CONSTRAINT `fk_commodity_tag_relation_commodity` FOREIGN KEY (`commodity_id`) REFERENCES `commodity` (`commodity_id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_commodity_tag_relation_tag` FOREIGN KEY (`tag_id`) REFERENCES `tag` (`tag_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '商品标签中转表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dining_table
-- ----------------------------
DROP TABLE IF EXISTS `dining_table`;
CREATE TABLE `dining_table`  (
  `dining_table_id` bigint NOT NULL AUTO_INCREMENT COMMENT '桌位ID',
  `table_no` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '桌号',
  `seat_count` int NOT NULL DEFAULT 0 COMMENT '可坐人数',
  `table_status_id` int NOT NULL DEFAULT 1 COMMENT '桌位状态ID',
  `qrcode_image_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '桌位二维码图片地址',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`dining_table_id`) USING BTREE,
  UNIQUE INDEX `uk_dining_table_no`(`table_no` ASC) USING BTREE,
  INDEX `idx_dining_table_status`(`table_status_id` ASC) USING BTREE,
  CONSTRAINT `fk_dining_table_status` FOREIGN KEY (`table_status_id`) REFERENCES `dining_table_status_dict` (`table_status_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '桌位表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dining_table_status_dict
-- ----------------------------
DROP TABLE IF EXISTS `dining_table_status_dict`;
CREATE TABLE `dining_table_status_dict`  (
  `table_status_id` int NOT NULL AUTO_INCREMENT COMMENT '桌位状态ID',
  `status_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`table_status_id`) USING BTREE,
  UNIQUE INDEX `uk_dining_table_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '桌位状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dish
-- ----------------------------
DROP TABLE IF EXISTS `dish`;
CREATE TABLE `dish`  (
  `dish_id` int NOT NULL AUTO_INCREMENT COMMENT '菜品ID',
  `dish_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '菜品名称',
  `dish_description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '菜品描述',
  `dish_price` decimal(10, 2) NOT NULL COMMENT '价格',
  `dish_category_id` int NOT NULL COMMENT '菜品分类ID',
  `image_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '图片地址',
  `attribute_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '属性名称',
  `status` int NOT NULL DEFAULT 1 COMMENT '上架状态 1上架 0下架',
  `limited_edition` int NOT NULL DEFAULT 0 COMMENT '限量供应',
  `dish_sold` int NOT NULL DEFAULT 0 COMMENT '已售数量',
  `dish_remaining_quantity` int NOT NULL DEFAULT 0 COMMENT '剩余数量',
  `user_purchase_limit` int NOT NULL DEFAULT 0 COMMENT '用户限购份数 0不限购',
  `dish_status_id` int NULL DEFAULT NULL COMMENT '菜品状态ID',
  `unit_id` int NULL DEFAULT NULL COMMENT '单位ID',
  `dish_qrcode_image` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '菜品二维码图片',
  PRIMARY KEY (`dish_id`) USING BTREE,
  INDEX `idx_dish_category_id`(`dish_category_id` ASC) USING BTREE,
  INDEX `idx_dish_status_id`(`dish_status_id` ASC) USING BTREE,
  INDEX `idx_dish_unit_id`(`unit_id` ASC) USING BTREE,
  INDEX `idx_dish_name`(`dish_name` ASC) USING BTREE,
  CONSTRAINT `fk_dish_dish_category` FOREIGN KEY (`dish_category_id`) REFERENCES `dish_category` (`dish_category_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_dish_status` FOREIGN KEY (`dish_status_id`) REFERENCES `dish_status` (`dish_status_id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_dish_unit` FOREIGN KEY (`unit_id`) REFERENCES `unit` (`unit_id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '菜品表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dish_category
-- ----------------------------
DROP TABLE IF EXISTS `dish_category`;
CREATE TABLE `dish_category`  (
  `dish_category_id` int NOT NULL AUTO_INCREMENT COMMENT '菜品分类ID',
  `dish_category_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '分类名称',
  `dish_sort_order` int NOT NULL DEFAULT 0 COMMENT '排序号',
  `dish_category_status_id` int NULL DEFAULT NULL COMMENT '菜品分类状态ID',
  PRIMARY KEY (`dish_category_id`) USING BTREE,
  UNIQUE INDEX `uk_dish_category_name`(`dish_category_name` ASC) USING BTREE,
  INDEX `idx_dish_category_status_id`(`dish_category_status_id` ASC) USING BTREE,
  INDEX `idx_dish_category_sort_order`(`dish_sort_order` ASC) USING BTREE,
  CONSTRAINT `fk_dish_category_dish_category_status` FOREIGN KEY (`dish_category_status_id`) REFERENCES `dish_category_status` (`dish_category_status_id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '菜品分类表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dish_category_status
-- ----------------------------
DROP TABLE IF EXISTS `dish_category_status`;
CREATE TABLE `dish_category_status`  (
  `dish_category_status_id` int NOT NULL AUTO_INCREMENT COMMENT '菜品分类状态ID',
  `status_name` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`dish_category_status_id`) USING BTREE,
  UNIQUE INDEX `uk_dish_category_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '菜品分类状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dish_image
-- ----------------------------
DROP TABLE IF EXISTS `dish_image`;
CREATE TABLE `dish_image`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '图片ID',
  `dish_id` int NOT NULL COMMENT '菜品ID',
  `image_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '图片地址',
  `sort_order` int NOT NULL DEFAULT 0 COMMENT '排序',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_dish_image_dish`(`dish_id` ASC, `sort_order` ASC) USING BTREE,
  CONSTRAINT `fk_dish_image_dish` FOREIGN KEY (`dish_id`) REFERENCES `dish` (`dish_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '菜品图片表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dish_order_details
-- ----------------------------
DROP TABLE IF EXISTS `dish_order_details`;
CREATE TABLE `dish_order_details`  (
  `dish_order_details_id` bigint NOT NULL AUTO_INCREMENT COMMENT '订餐明细ID',
  `dish_order_id` bigint NOT NULL COMMENT '订餐订单ID',
  `dish_id` int NOT NULL COMMENT '菜品ID',
  `unit_price` decimal(10, 2) NOT NULL COMMENT '菜品单价',
  `quantity` int NOT NULL COMMENT '订购数量',
  `subtotal_amount` decimal(10, 2) NOT NULL COMMENT '小计金额',
  `status_id` int NULL DEFAULT NULL COMMENT '出餐状态ID',
  PRIMARY KEY (`dish_order_details_id`) USING BTREE,
  INDEX `idx_dish_order_details_order_id`(`dish_order_id` ASC) USING BTREE,
  INDEX `idx_dish_order_details_dish_id`(`dish_id` ASC) USING BTREE,
  INDEX `idx_dish_order_details_status_id`(`status_id` ASC) USING BTREE,
  CONSTRAINT `fk_dish_order_details_dish` FOREIGN KEY (`dish_id`) REFERENCES `dish` (`dish_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_dish_order_details_order` FOREIGN KEY (`dish_order_id`) REFERENCES `dish_orders` (`order_id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_dish_order_details_status` FOREIGN KEY (`status_id`) REFERENCES `dish_status` (`dish_status_id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '餐品订单明细表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dish_order_status
-- ----------------------------
DROP TABLE IF EXISTS `dish_order_status`;
CREATE TABLE `dish_order_status`  (
  `order_status_id` int NOT NULL AUTO_INCREMENT COMMENT '餐品订单状态ID',
  `status_name` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '状态名称',
  PRIMARY KEY (`order_status_id`) USING BTREE,
  UNIQUE INDEX `uk_dish_order_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '餐品订单状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dish_orders
-- ----------------------------
DROP TABLE IF EXISTS `dish_orders`;
CREATE TABLE `dish_orders`  (
  `order_id` bigint NOT NULL AUTO_INCREMENT COMMENT '餐品订单ID',
  `order_no` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '平台订单号',
  `wx_pay_no` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '微信支付订单号',
  `total_amount` decimal(10, 2) NOT NULL COMMENT '订单总金额',
  `total_quantity` int NOT NULL COMMENT '订单菜品总数',
  `order_status_id` int NOT NULL COMMENT '订单状态ID',
  `user_id` int NOT NULL COMMENT '下单人ID',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '下单时间',
  `dining_table_id` bigint NOT NULL COMMENT '桌台ID',
  PRIMARY KEY (`order_id`) USING BTREE,
  UNIQUE INDEX `uk_dish_orders_order_no`(`order_no` ASC) USING BTREE,
  INDEX `idx_dish_orders_status_id`(`order_status_id` ASC) USING BTREE,
  INDEX `idx_dish_orders_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_dish_orders_dining_table_id`(`dining_table_id` ASC) USING BTREE,
  INDEX `idx_dish_orders_create_time`(`create_time` ASC) USING BTREE,
  CONSTRAINT `fk_dish_orders_dining_table` FOREIGN KEY (`dining_table_id`) REFERENCES `dining_table` (`dining_table_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_dish_orders_status` FOREIGN KEY (`order_status_id`) REFERENCES `dish_order_status` (`order_status_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_dish_orders_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '餐品订单表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for dish_status
-- ----------------------------
DROP TABLE IF EXISTS `dish_status`;
CREATE TABLE `dish_status`  (
  `dish_status_id` int NOT NULL AUTO_INCREMENT COMMENT '菜品状态ID',
  `status_name` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '菜品状态名称',
  PRIMARY KEY (`dish_status_id`) USING BTREE,
  UNIQUE INDEX `uk_dish_status_name`(`status_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '菜品出餐状态表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for role
-- ----------------------------
DROP TABLE IF EXISTS `role`;
CREATE TABLE `role`  (
  `role_id` int NOT NULL AUTO_INCREMENT COMMENT '角色ID',
  `role_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '角色名称',
  PRIMARY KEY (`role_id`) USING BTREE,
  UNIQUE INDEX `uk_role_name`(`role_name` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 2 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '角色表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for shipping_address
-- ----------------------------
DROP TABLE IF EXISTS `shipping_address`;
CREATE TABLE `shipping_address`  (
  `address_id` bigint NOT NULL AUTO_INCREMENT COMMENT '收货地址ID',
  `user_id` int NOT NULL COMMENT '用户ID',
  `contact_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '联系人姓名',
  `contact_phone` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '' COMMENT '联系电话',
  `province` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '省份',
  `city` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '市',
  `municipal_district` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '区/县',
  `addres` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '详细地址',
  `is_default` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否默认地址',
  PRIMARY KEY (`address_id`) USING BTREE,
  INDEX `idx_shipping_address_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_shipping_address_default`(`user_id` ASC, `is_default` ASC) USING BTREE,
  CONSTRAINT `fk_shipping_address_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '收货地址表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for shipping_cart
-- ----------------------------
DROP TABLE IF EXISTS `shipping_cart`;
CREATE TABLE `shipping_cart`  (
  `shipping_cart_id` int NOT NULL AUTO_INCREMENT COMMENT '购物车ID',
  `user_id` int NOT NULL COMMENT '用户ID',
  `cart_item_type` int NULL DEFAULT NULL COMMENT '购物车项目类型 1商品 2菜品 3活动 4认养',
  `universal_id` bigint NULL DEFAULT NULL COMMENT '通用项目ID',
  `cart_quantity` int NOT NULL DEFAULT 1 COMMENT '购买数量',
  PRIMARY KEY (`shipping_cart_id`) USING BTREE,
  INDEX `idx_shipping_cart_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_shipping_cart_type_item`(`cart_item_type` ASC, `universal_id` ASC) USING BTREE,
  INDEX `idx_shipping_cart_user_type_item`(`user_id` ASC, `cart_item_type` ASC, `universal_id` ASC) USING BTREE,
  CONSTRAINT `fk_shipping_cart_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '购物车表（多业务通用，universal_id 不设置外键）' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for stock_log
-- ----------------------------
DROP TABLE IF EXISTS `stock_log`;
CREATE TABLE `stock_log`  (
  `stock_log_id` int NOT NULL AUTO_INCREMENT COMMENT '库存日志ID',
  `commodity_id` int NOT NULL COMMENT '商品ID',
  `change_quantity` int NOT NULL COMMENT '库存变更数量',
  `before_stock` int NOT NULL COMMENT '变更前库存',
  `after_stock` int NOT NULL COMMENT '变更后库存',
  `user_id` int NOT NULL COMMENT '操作人用户ID',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`stock_log_id`) USING BTREE,
  INDEX `idx_stock_log_commodity_id`(`commodity_id` ASC) USING BTREE,
  INDEX `idx_stock_log_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_stock_log_create_time`(`create_time` ASC) USING BTREE,
  CONSTRAINT `fk_stock_log_commodity` FOREIGN KEY (`commodity_id`) REFERENCES `commodity` (`commodity_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_stock_log_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '库存修改日志表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for tag
-- ----------------------------
DROP TABLE IF EXISTS `tag`;
CREATE TABLE `tag`  (
  `tag_id` int NOT NULL AUTO_INCREMENT COMMENT '标签ID',
  `tag_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '标签名称',
  PRIMARY KEY (`tag_id`) USING BTREE,
  UNIQUE INDEX `uk_tag_name`(`tag_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '标签表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for tracking_type
-- ----------------------------
DROP TABLE IF EXISTS `tracking_type`;
CREATE TABLE `tracking_type`  (
  `tracking_type_id` bigint NOT NULL AUTO_INCREMENT COMMENT '物流类型ID',
  `tracking_type_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '物流类型名称',
  PRIMARY KEY (`tracking_type_id`) USING BTREE,
  UNIQUE INDEX `uk_tracking_type_name`(`tracking_type_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '物流类型表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for unit
-- ----------------------------
DROP TABLE IF EXISTS `unit`;
CREATE TABLE `unit`  (
  `unit_id` int NOT NULL AUTO_INCREMENT COMMENT '单位ID',
  `unit_name` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '单位名称',
  `unit_code` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '单位编码',
  `is_enabled` tinyint NOT NULL DEFAULT 1 COMMENT '1启用 0禁用',
  PRIMARY KEY (`unit_id`) USING BTREE,
  UNIQUE INDEX `uk_unit_code`(`unit_code` ASC) USING BTREE,
  UNIQUE INDEX `uk_unit_name`(`unit_name` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '单位表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for user
-- ----------------------------
DROP TABLE IF EXISTS `user`;
CREATE TABLE `user`  (
  `user_id` int NOT NULL AUTO_INCREMENT COMMENT '用户ID',
  `user_guid` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '用户唯一标识',
  `phone_number` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '手机号',
  `register_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '注册时间',
  `wx_openid` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '微信OpenID',
  `wx_image` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '微信头像',
  `wx_nickname` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '微信昵称',
  `real_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '用户姓名',
  `password_hash` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '' COMMENT '密码哈希值',
  `gender` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT '保密' COMMENT '性别',
  `role_id` int NOT NULL COMMENT '角色ID',
  PRIMARY KEY (`user_id`) USING BTREE,
  UNIQUE INDEX `uk_user_guid`(`user_guid` ASC) USING BTREE,
  UNIQUE INDEX `uk_user_wx_openid`(`wx_openid` ASC) USING BTREE,
  INDEX `idx_user_role_id`(`role_id` ASC) USING BTREE,
  INDEX `idx_user_phone_number`(`phone_number` ASC) USING BTREE,
  CONSTRAINT `fk_user_role` FOREIGN KEY (`role_id`) REFERENCES `role` (`role_id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 2 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '用户表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for videos
-- ----------------------------
DROP TABLE IF EXISTS `videos`;
CREATE TABLE `videos`  (
  `video_id` bigint NOT NULL AUTO_INCREMENT COMMENT '视频ID',
  `video_url` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '视频URL',
  `position` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT 'home' COMMENT '位置 home/goods/acres',
  `sort_order` int NOT NULL DEFAULT 0 COMMENT '排序',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`video_id`) USING BTREE,
  INDEX `idx_videos_pos_sort`(`position` ASC, `sort_order` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '视频表' ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;
