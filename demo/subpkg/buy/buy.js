// pages/buy/buy.js
const api = require('../../utils/api');

Page({

    /**
     * 页面的初始数据
     */
    data: {
        selectedPayment: 0, // 默认选择微信支付
        totalAmount: '0.00', // 合计金额
        deliveryType: 'delivery', // 默认快递地址
        showAddressForm: false, // 地址表单显示状态
        addressInfo: {
            name: '',
            phone: '',
            detail: '',
            door: '',
            pickupTime: ''
        }, // 地址信息
        hasAddress: false // 是否已填写地址
    },

    /**
     * 生命周期函数--监听页面加载
     */
    onLoad(options) {
        console.log('options:', options);
        // 从购物车页面传递过来的订单信息
        if (options && options.orderId) {
            // 这里可以根据orderId获取订单详情
            this.loadOrderInfo(options.orderId);
        }
    },

    /**
     * 加载订单信息
     */
    loadOrderInfo(orderId) {
        wx.showLoading({
            title: '加载订单信息...'
        });
        
        api.request({
            url: '/api/order/info',
            method: 'GET',
            data: {
                orderId: orderId
            }
        })
        .then((data) => {
            wx.hideLoading();
            if (data) {
                this.setData({
                    totalAmount: data.totalAmount || '0.00'
                });
            }
        })
        .catch((err) => {
            wx.hideLoading();
            console.error('获取订单信息失败:', err);
        });
    },

    /**
     * 选择配送方式
     */
    selectDeliveryType(e) {
        const type = e.currentTarget.dataset.type;
        this.setData({
            deliveryType: type
        });
    },

    /**
     * 切换地址表单显示/隐藏
     */
    toggleAddressForm() {
        this.setData({
            showAddressForm: !this.data.showAddressForm
        });
    },

    /**
     * 处理地址输入
     */
    bindAddressInput(e) {
        const field = e.currentTarget.dataset.field;
        const value = e.detail.value;
        const addressInfo = {...this.data.addressInfo};
        addressInfo[field] = value;
        this.setData({
            addressInfo: addressInfo
        });
    },

    /**
     * 获取当前位置
     */
    getLocation() {
        wx.chooseLocation({
            success: (res) => {
                console.log('选择位置成功:', res);
                const addressInfo = {...this.data.addressInfo};
                addressInfo.detail = res.address;
                this.setData({
                    addressInfo: addressInfo
                });
                wx.showToast({
                    title: '位置获取成功',
                    icon: 'success'
                });
            },
            fail: (err) => {
                console.error('选择位置失败:', err);
                wx.showToast({
                    title: '位置获取失败',
                    icon: 'none'
                });
            }
        });
    },

    /**
     * 选择取货时间
     */
    selectPickupTime(e) {
        const time = e.currentTarget.dataset.time;
        const addressInfo = {...this.data.addressInfo};
        addressInfo.pickupTime = time;
        this.setData({
            addressInfo: addressInfo
        });
    },

    /**
     * 保存地址
     */
    saveAddress() {
        const {name, phone, detail, pickupTime} = this.data.addressInfo;
        const {deliveryType} = this.data;
        
        // 验证快递地址信息
        if (deliveryType === 'delivery') {
            if (!name || !phone || !detail) {
                wx.showToast({
                    title: '请填写完整地址信息',
                    icon: 'none'
                });
                return;
            }
        } else {
            // 验证到店自提信息
            if (!name || !phone || !pickupTime) {
                wx.showToast({
                    title: '请填写收货人、手机号和取货时间',
                    icon: 'none'
                });
                return;
            }
        }
        
        // 保存地址逻辑
        this.setData({
            hasAddress: true,
            showAddressForm: false
        });
        
        wx.showToast({
            title: '地址保存成功',
            icon: 'success'
        });
    },

    /**
     * 选择支付方式
     */
    selectPayment(e) {
        const index = e.currentTarget.dataset.index;
        this.setData({
            selectedPayment: index
        });
    },

    /**
     * 确认支付
     */
    confirmPayment() {
        // 检查是否填写了地址
        if (!this.data.hasAddress) {
            wx.showToast({
                title: '请先填写收货地址',
                icon: 'none'
            });
            return;
        }
        
        // 这里可以实现提交订单的逻辑
        wx.showToast({
            title: '订单提交成功',
            icon: 'success'
        });
        // 跳转到支付结果页面
        setTimeout(() => {
            wx.navigateBack();
        }, 1500);
    },

    /**
     * 返回上一页
     */
    goBack() {
        wx.navigateBack();
    }
});