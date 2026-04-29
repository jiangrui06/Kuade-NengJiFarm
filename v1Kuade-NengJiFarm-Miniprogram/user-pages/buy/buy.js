// pages/buy/buy.js
const api = require('../../utils/api');

Page({
    data: {
        selectedPayment: 0, // 默认选择微信支付
        totalAmount: '0.00', // 总计金额
        deliveryType: 'delivery', // 默认快递到家
        showAddressForm: false, // 地址选择状态
        showAddAddressFormFlag: false, // 是否显示添加地址
        addressInfo: {
            name: '',
            phone: '',
            detail: '',
            door: '',
            pickupTime: ''
        }, // 地址信息
        hasAddress: false, // 是否有地址
        addressList: [], // 用户地址列表
        selectedAddressId: null, // 选中的地址ID
        products: [] // 商品列表
    },

    onLoad(options) {
        console.log('buy page options:', options);
        if (options && options.orderId) {
            this.loadOrderInfo(options.orderId);
        }
        this.getUserAddressList();
    },

    getUserAddressList() {
        api.user.getAddresses()
            .then((addresses) => {
                console.log('用户地址列表:', addresses);
                if (addresses && addresses.length > 0) {
                    const defaultAddress = addresses.find(a => a.isDefault) || addresses[0];
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
            });
    },

    loadOrderInfo(orderId) {
        wx.showLoading({
            title: '加载中...'
        });
        
        api.order.getDetail(orderId)
        .then((data) => {
            wx.hideLoading();
            if (data) {
                this.setData({
                    totalAmount: data.totalPrice || '0.00',
                    products: data.items || []
                });
            }
        })
        .catch((err) => {
            wx.hideLoading();
            console.error('获取订单信息失败:', err);
        });
    },

    selectDeliveryType(e) {
        const type = e.currentTarget.dataset.type;
        this.setData({
            deliveryType: type
        });
    },

    toggleAddressForm() {
        this.setData({
            showAddressForm: !this.data.showAddressForm,
            showAddAddressFormFlag: false
        });
    },

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

    bindAddressInput(e) {
        const field = e.currentTarget.dataset.field;
        const value = e.detail.value;
        const addressInfo = {...this.data.addressInfo};
        addressInfo[field] = value;
        this.setData({
            addressInfo: addressInfo
        });
    },

    submitOrder() {
        if (!this.data.hasAddress && this.data.deliveryType === 'delivery') {
            wx.showToast({ title: '请选择收货地址', icon: 'none' });
            return;
        }

        // 提交订单逻辑...
        wx.showToast({ title: '功能开发中', icon: 'none' });
    }
});

