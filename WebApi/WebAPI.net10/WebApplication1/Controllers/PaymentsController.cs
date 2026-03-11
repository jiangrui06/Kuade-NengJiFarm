using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        // Used by: demo/pages/cart/cart.js or demo/pages/order/order.js (发起微信支付)
        #region CreateWechatPayment - demo/pages/cart/cart.js, demo/pages/order/order.js发起微信支付
        [HttpPost("wechat")]
        public ActionResult<ApiResponse<object>> CreateWechatPayment([FromBody] object body)
        {
            // return parameters needed by mini program to invoke payment
            var data = new { PrepayId = "", PaySign = "" };
            return ApiResponse<object>.Ok(data);
        }
        #endregion

        // Used by: payment gateway callback (无前端调用文件)
        #region Notify - payment gateway
        [HttpPost("wechat/notify")]
        public ActionResult<string> Notify()
        {
            // Payment gateway will POST here. Return plain text success acknowledgement.
            return "success";
        }
        #endregion
    }
}
