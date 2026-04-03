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
        showAddAddressFormFlag: false, // 是否显示添加地址表单
        addressInfo: {
            name: '',
            phone: '',
            detail: '',
            door: '',
            pickupTime: ''
        }, // 地址信息
        hasAddress: false, // 是否已填写地址
        addressList: [], // 用户地址列表
        selectedAddressId: null, // 选中的地址ID
        products: [
            {
                id: '1',
                name: '新鲜蔬菜',
                price: 19.9,
                quantity: 2
            },
            {
                id: '2',
                name: '有机水果',
                price: 29.9,
                quantity: 1
            }
        ] 
    },

    /**
     * 生命周期函数--监听页面加载
     */
    onLoad(options) {
        console.log('options:', options);
        // 计算合计金额
        this.calculateTotalAmount();
        // 从购物车页面传递过来的订单信息
        if (options && options.orderId) {
            // 这里可以根据orderId获取订单详情
            this.loadOrderInfo(options.orderId);
        }
        // 获取用户地址列表
        this.getUserAddressList();
    },

    /**
     * 获取用户地址列表
     */
    getUserAddressList() {
        api.api.user.getAddressList()
            .then((addresses) => {
                console.log('用户地址列表:', addresses);
                if (addresses && addresses.length > 0) {
                    // 选择第一个地址作为默认地址
                    const defaultAddress = addresses[0];
                    this.setData({
                        addressList: addresses,
                        selectedAddressId: defaultAddress.id,
                        addressInfo: {
                            name: defaultAddress.name,
                            phone: defaultAddress.phone,
                            detail: defaultAddress.address,
                            door: defaultAddress.door || '',
                            pickupTime: ''
                        },
                        hasAddress: true
                    });
                }
            })
            .catch((err) => {
                console.error('获取地址列表失败:', err);
                // 如果获取失败，不影响页面正常显示
            });
    },

    /**
     * 计算合计金额
     */
    calculateTotalAmount() {
        const products = this.data.products;
        let total = 0;
        products.forEach(product => {
            total += product.price * product.quantity;
        });
        this.setData({
            totalAmount: total.toFixed(2)
        });
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
            showAddressForm: !this.data.showAddressForm,
            showAddAddressFormFlag: false
        });
    },

    /**
     * 选择地址
     */
    selectAddress(e) {
        const addressId = e.currentTarget.dataset.id;
        const addressList = this.data.addressList;
        const selectedAddress = addressList.find(item => item.id === addressId);
        
        if (selectedAddress) {
            this.setData({
                selectedAddressId: addressId,
                addressInfo: {
                    name: selectedAddress.name,
                    phone: selectedAddress.phone,
                    detail: selectedAddress.address,
                    door: selectedAddress.door || '',
                    pickupTime: ''
                },
                hasAddress: true,
                showAddressForm: false
            });
        }
    },

    /**
     * 显示添加地址表单
     */
    showAddAddressForm() {
        this.setData({
            showAddAddressFormFlag: true
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
        const {name, phone, detail, door, pickupTime} = this.data.addressInfo;
        const {deliveryType} = this.data;
        
        // 验证手机号
        if (!phone) {
            wx.showToast({
                title: '请输入手机号',
                icon: 'none'
            });
            return;
        }
        
        // 验证手机号格式
        const phoneRegex = /^1[3-9]\d{9}$/;
        if (!phoneRegex.test(phone)) {
            wx.showToast({
                title: '请输入11位有效手机号',
                icon: 'none'
            });
            return;
        }
        
        // 验证快递地址信息
        if (deliveryType === 'delivery') {
            if (!name || !detail) {
                wx.showToast({
                    title: '请填写完整地址信息',
                    icon: 'none'
                });
                return;
            }
        } else {
            // 验证到店自提信息
            if (!name || !pickupTime) {
                wx.showToast({
                    title: '请填写收货人、手机号和取货时间',
                    icon: 'none'
                });
                return;
            }
        }
        
        // 保存地址到服务器
        const addressData = {
            name: name,
            phone: phone,
            address: detail,
            door: door
        };
        
        // 对于到店自提，添加取货时间
        if (deliveryType === 'pickup') {
            addressData.pickupTime = pickupTime;
        }
        
        console.log('保存地址数据:', addressData);
        
        api.api.user.addAddress(addressData)
            .then((res) => {
                console.log('添加地址成功:', res);
                // 刷新地址列表
                this.getUserAddressList();
                this.setData({
                    showAddressForm: false
                });
                
                wx.showToast({
                    title: '地址保存成功',
                    icon: 'success'
                });
            })
            .catch((err) => {
                console.error('添加地址失败:', err);
                // 显示更详细的错误信息
                const errorMsg = err && err.message ? err.message : '地址保存失败，请重试';
                wx.showToast({
                    title: errorMsg,
                    icon: 'none'
                });
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