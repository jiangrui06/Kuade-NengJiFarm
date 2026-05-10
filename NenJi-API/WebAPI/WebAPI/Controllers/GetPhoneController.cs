using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Text.Json;

namespace MiniProgramServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GetPhoneController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public GetPhoneController(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        public class PhoneCodeRequest
        {
            public string Code { get; set; } = string.Empty;
        }
        /// <summary>
        /// 请求微信接口获取手机号，前端传入code参数，后端使用code换取access_token，再使用access_token获取手机号信息
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("phone_number")]
        public async Task<IActionResult> GetPhoneNumber([FromBody] PhoneCodeRequest request)
        {
            if (string.IsNullOrEmpty(request.Code))
                return BadRequest(new { msg = "code不能为空" });

            try
            {
                string? appId = _config["WeChat:AppId"];
                string? appSecret = _config["WeChat:AppSecret"];
                if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
                {
                    return StatusCode(500, new { msg = "微信配置缺失" });
                }

                string tokenUrl = $"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={appId}&secret={appSecret}";

                var tokenResp = await _httpClient.GetAsync(tokenUrl);
                tokenResp.EnsureSuccessStatusCode(); // 确保请求成功
                var tokenJson = await tokenResp.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

                if (tokenData.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() != 0)
                {
                    string errMsg = tokenData.GetProperty("errmsg").GetString() ?? "获取token失败";
                    return BadRequest(new { msg = errMsg });
                }
                string accessToken = tokenData.GetProperty("access_token").GetString()!;

                string phoneUrl = $"https://api.weixin.qq.com/wxa/business/getuserphonenumber?access_token={accessToken}";
                var postData = new { code = request.Code };
                var content = new StringContent(
                    JsonSerializer.Serialize(postData),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var phoneResp = await _httpClient.PostAsync(phoneUrl, content);
                phoneResp.EnsureSuccessStatusCode();
                var phoneJson = await phoneResp.Content.ReadAsStringAsync();
                var phoneData = JsonSerializer.Deserialize<JsonElement>(phoneJson);

                int phoneErrCode = phoneData.GetProperty("errcode").GetInt32();
                if (phoneErrCode != 0)
                {
                    string phoneErrMsg = phoneData.GetProperty("errmsg").GetString() ?? "获取手机号失败";
                    return BadRequest(new { msg = phoneErrMsg });
                }

                var phoneInfo = phoneData.GetProperty("phone_info");
                return Ok(new
                {
                    success = true,
                    phoneNumber = phoneInfo.GetProperty("phoneNumber").GetString(),
                    purePhoneNumber = phoneInfo.GetProperty("purePhoneNumber").GetString()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { msg = $"服务器错误：{ex.Message}" });
            }
        }
    }
}
