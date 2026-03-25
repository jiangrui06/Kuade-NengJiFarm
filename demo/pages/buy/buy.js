// pages/buy/buy.js
Page({

    /**
     * 页面的初始数据
     */
    data: {
        selectedPayment: 0 // 默认选择钱包余额支付
    },

    /**
     * 生命周期函数--监听页面加载
     */
    onLoad(options) {

    },

    /**
     * 选择支付方式
     */
    selectPayment(e) {
        console.log('点击了支付方式', e.currentTarget.dataset);
        const index = e.currentTarget.dataset.index;
        console.log('选择的支付方式索引：', index);
        this.setData({
            selectedPayment: index
        }, () => {
            console.log('更新后的selectedPayment：', this.data.selectedPayment);
        });
    },

    /**
     * 确认支付
     */
    confirmPayment() {
        wx.showToast({
            title: '支付成功！',
            icon: 'success',
            duration: 2000
        });
        // 这里可以添加支付逻辑
        setTimeout(() => {
            wx.navigateBack();
        }, 2000);
    },

    /**
     * 返回上一页
     */
    goBack() {
        wx.navigateBack();
    }
})