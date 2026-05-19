-- ====================================================
-- 券类订单退款功能 - 数据库迁移脚本
-- 说明：新增退款记录表 + 补充订单状态
-- ====================================================

-- 1. 创建退款记录表（用于所有类型的退款记录）
CREATE TABLE IF NOT EXISTS `refund_record` (
    `refund_id`    BIGINT AUTO_INCREMENT PRIMARY KEY COMMENT '自增主键',
    `refund_no`    VARCHAR(64) NOT NULL COMMENT '退款流水号（如 RF202605191430123456）',
    `order_id`     BIGINT NOT NULL COMMENT '订单ID',
    `order_no`     VARCHAR(64) NOT NULL COMMENT '订单号',
    `order_type`   VARCHAR(20) NOT NULL COMMENT '订单类型（activity=券类, commodity=商品）',
    `user_id`      INT NOT NULL COMMENT '用户ID',
    `reason`       VARCHAR(50) DEFAULT '' COMMENT '退款原因编码',
    `description`  VARCHAR(500) DEFAULT NULL COMMENT '退款描述/备注',
    `images`       TEXT DEFAULT NULL COMMENT '退款凭证图片',
    `refund_amount` DECIMAL(10,2) NOT NULL COMMENT '退款金额',
    `status`       VARCHAR(20) NOT NULL DEFAULT 'pending' COMMENT '退款状态（pending=处理中, completed=已完成, failed=失败）',
    `process_time` DATETIME DEFAULT NULL COMMENT '处理时间',
    `process_note` VARCHAR(500) DEFAULT NULL COMMENT '处理备注',
    `admin_reply`  VARCHAR(500) DEFAULT NULL COMMENT '管理员回复/操作人',
    `create_time`  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_time`  DATETIME DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    INDEX `idx_refund_order_no` (`order_no`),
    INDEX `idx_refund_order_type` (`order_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='退款记录表';

-- 2. 补充订单状态：已退款（ID=4）
INSERT IGNORE INTO `activity_order_status` (`activity_order_status_id`, `status_name`)
VALUES (4, '已退款');
