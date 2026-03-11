using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MySqlConnector;

namespace WXApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : Controller
    {
        private readonly IConfiguration _config;

        public LoginController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 微信小程序自动登录：客户端调用 wx.login 得到 code 后，将 code 发到此接口。
        /// 服务器调用 jscode2session 接口换取 openid 与 session_key，并返回给客户端（及一个简单的 server token）。
        /// 请在 appsettings.json 中配置 WeChat:AppId 与 WeChat:Secret
        /// </summary>
        [HttpPost("wxlogin")]
        public async Task<IActionResult> WxLogin([FromBody] WxLoginRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Code))
                return BadRequest(new { error = "code required" });

            var appId = _config["WeChat:AppId"];
            var secret = _config["WeChat:Secret"];
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret))
                return StatusCode(500, new { error = "WeChat AppId/Secret not configured" });

            var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={Uri.EscapeDataString(appId)}&secret={Uri.EscapeDataString(secret)}&js_code={Uri.EscapeDataString(req.Code)}&grant_type=authorization_code";

            try
            {
                using var client = new HttpClient();
                var resp = await client.GetStringAsync(url);
                var session = JsonSerializer.Deserialize<WxSessionResponse>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (session == null)
                    return StatusCode(502, new { error = "invalid response from wechat" });

                if (!string.IsNullOrEmpty(session.ErrMsg) || (session.ErrCode.HasValue && session.ErrCode.Value != 0))
                {
                    return BadRequest(new { session.ErrCode, session.ErrMsg });
                }

                // Create a simple server token (replace with real token generation / user persistence in production)
                var tokenPayload = $"{session.OpenId}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenPayload));

                // Try to insert user record into MySQL. Connection string is read from appsettings.json: ConnectionStrings:MySql
                var mysqlConn = _config.GetConnectionString("MySql");
                bool dbOk = false;
                if (!string.IsNullOrWhiteSpace(mysqlConn))
                {
                    try
                    {
                        await using var conn = new MySqlConnection(mysqlConn);
                        await conn.OpenAsync();

                        // Upsert user according to your existing schema:
                        // (id INT AUTO_INCREMENT PRIMARY KEY,
                        //  openid VARCHAR(100) NOT NULL UNIQUE,
                        //  nickname VARCHAR(100),
                        //  avatar VARCHAR(255),
                        //  created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                        //  last_login DATETIME)

                        var openidToInsert = string.IsNullOrWhiteSpace(session.OpenId) ? "1" : session.OpenId;
                        var nicknameParam = string.IsNullOrWhiteSpace(req.Nickname) ? (object)DBNull.Value : req.Nickname;
                        var avatarParam = string.IsNullOrWhiteSpace(req.Avatar) ? (object)DBNull.Value : req.Avatar;

                        var upsertSql = @"INSERT INTO users (openid, nickname, avatar, last_login)
                                          VALUES (@openid, @nickname, @avatar, @lastLogin)
                                          ON DUPLICATE KEY UPDATE
                                            last_login = VALUES(last_login),
                                            nickname = COALESCE(VALUES(nickname), nickname),
                                            avatar = COALESCE(VALUES(avatar), avatar);";

                        await using (var cmd = new MySqlCommand(upsertSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@openid", openidToInsert);
                            cmd.Parameters.AddWithValue("@nickname", nicknameParam);
                            cmd.Parameters.AddWithValue("@avatar", avatarParam);
                            cmd.Parameters.AddWithValue("@lastLogin", DateTime.UtcNow);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Retrieve the user id
                        long userId = 0;
                        var selectSql = "SELECT id FROM users WHERE openid = @openid LIMIT 1;";
                        await using (var cmdSel = new MySqlCommand(selectSql, conn))
                        {
                            cmdSel.Parameters.AddWithValue("@openid", openidToInsert);
                            var idObj = await cmdSel.ExecuteScalarAsync();
                            if (idObj != null && idObj != DBNull.Value)
                                userId = Convert.ToInt64(idObj);
                        }

                        dbOk = true;
                    }
                    catch
                    {
                        // swallow DB errors; we still return login result. In production, log this.
                        dbOk = false;
                    }
                }

                return Ok(new
                {
                    openid = session.OpenId,
                    session_key = session.SessionKey,
                    unionid = session.UnionId,
                    token,
                    db_written = dbOk
                });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = "failed to call wechat api", detail = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class WxLoginRequest
        {
            public string Code { get; set; }
            public string? Nickname { get; set; }
            public string? Avatar { get; set; }
        }

        public class WxSessionResponse
        {
            public string OpenId { get; set; }
            public string SessionKey { get; set; }
            public string UnionId { get; set; }
            public int? ErrCode { get; set; }
            public string ErrMsg { get; set; }
        }
    }
}