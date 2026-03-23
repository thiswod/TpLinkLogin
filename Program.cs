using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using WodToolKit.Http;

try
{
    // 初始化路由器登录助手（路由器IP为实际IP）
    var loginHelper = new TplinkLoginHelper("192.168.0.1");
    // 原始密码
    string rawPwd = "REDACTED";
    // 构建加密后的登录请求参数
    string loginRequestJson = loginHelper.BuildLoginRequest(rawPwd);

    Console.WriteLine("加密后的登录请求参数：");
    Console.WriteLine(loginRequestJson);

    // 后续可将loginRequestJson发送到路由器http://192.168.1.1/，完成登录
}
catch (Exception ex)
{
    Console.WriteLine("错误：" + ex.Message);
}


public class TplinkLoginHelper
{
    private readonly string _routerIp; // 路由器IP，如192.168.1.1
    private const string CONTENT_TYPE = "application/json";
    private const int ENONE = 0; // 成功错误码

    public TplinkLoginHelper(string routerIp)
    {
        _routerIp = routerIp;
    }

    /// <summary>
    /// 获取路由器加密信息（对应getEncryptInfo）
    /// </summary>
    /// <returns>加密信息实体</returns>
    public EncryptInfoResponse GetEncryptInfo()
    {
        try
        {
            string url = $"http://{_routerIp}/";
            var request = new HttpRequestClass();
            request.SetTimeout(30);
            request.SetUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var jsonNull = System.Text.Json.JsonSerializer.SerializeToElement<object?>(null);
            var requestBody = new Dictionary<string, object?>
            {
                ["method"] = "do",
                ["user_management"] = new Dictionary<string, object?>
                {
                    ["get_encrypt_info"] = jsonNull
                }
            };

            request.Open(url, HttpMethod.Post).Send(requestBody);
            var responseData = request.GetResponse();

            if (responseData.StatusCode != 200)
            {
                throw new Exception($"HTTP请求失败，状态码: {responseData.StatusCode}，响应: {responseData.Body}");
            }

            var encryptInfo = JsonConvert.DeserializeObject<EncryptInfoResponse>(responseData.Body);
            if (encryptInfo == null)
            {
                throw new Exception("响应解析失败");
            }

            if (encryptInfo.ErrCode == ENONE && !string.IsNullOrEmpty(encryptInfo.Key))
            {
                encryptInfo.Key = TplinkEncryptUtil.UrlDecode(encryptInfo.Key);
            }

            return encryptInfo;
        }
        catch (Exception ex)
        {
            throw new Exception("获取加密信息失败：" + ex.Message, ex);
        }
    }

    /// <summary>
        /// 对原始密码进行加密（自动根据加密类型选择算法）
        /// </summary>
        /// <param name="rawPwd">原始密码</param>
        /// <param name="encryptInfo">加密信息</param>
        /// <returns>加密后的密码 + 加密类型</returns>
        public (string EncryptedPwd, string EncryptType) EncryptPassword(string rawPwd, EncryptInfoResponse encryptInfo)
        {
            if (encryptInfo == null || encryptInfo.ErrCode != ENONE)
            {
                throw new ArgumentException("加密信息无效，请先成功获取加密信息");
            }

            // 判断是否为MD5加密（encrypt_type包含3）
            if (encryptInfo.EncryptType != null && Array.Exists(encryptInfo.EncryptType, t => t == "3"))
            {
                // MD5加密分支：pwd + ":" + nonce → MD5
                string saltPwd = $"{rawPwd}:{encryptInfo.Nonce}";
                string md5Pwd = TplinkEncryptUtil.Md5Hash(saltPwd);
                return (md5Pwd, "3");
            }
            else
            {
                // 自定义对称加密分支
                string customPwd = TplinkEncryptUtil.CustomEncrypt(rawPwd);
                return (customPwd, string.Empty);
            }
        }

    /// <summary>
    /// 构建登录请求参数
    /// </summary>
    /// <param name="rawPwd">原始密码</param>
    /// <returns>登录请求JSON字符串</returns>
    public string BuildLoginRequest(string rawPwd)
    {
        var encryptInfo = GetEncryptInfo();
        var (encryptedPwd, encryptType) = EncryptPassword(rawPwd, encryptInfo);

        var loginRequest = new LoginRequest();
        loginRequest.Login.Password = encryptedPwd;
        if (!string.IsNullOrEmpty(encryptType))
        {
            loginRequest.Login.EncryptType = encryptType;
        }

        return JsonConvert.SerializeObject(loginRequest);
    }
}


public static class TplinkEncryptUtil
{
    // 自定义对称加密固定参数（与JS中一致）
    private const string FIX_STR = "RDpbLfCPsJZ7fiv";
    private const string KEY_STR = "yLwVl0zKqws7LgKPRQ84Mdt708T1qQ3Ha7xv3H7NyU84p21BriUWBU43odz3iP4rBL3cD02KZciXTysVXiV8ngg6vL48rPJyAUw0HurW20xqxv9aYb4M9wK1Ae0wlro510qXeU07kV57fQMc8L6aLgMLwygtc0F10a0Dg70TOoouyFhdysuRMO51yY5ZlOZZLEal1h0t9YQW0Ko7oBwmCAHoic4HYbUyVeU3sfQ1xtXcPcf1aT303wAQhv66qzW";

    /// <summary>
    /// 自定义对称加密（对应orgAuthPwd）
    /// </summary>
    /// <param name="pwd">原始密码</param>
    /// <returns>加密后的密码</returns>
    public static string CustomEncrypt(string pwd)
    {
        StringBuilder result = new StringBuilder();
        int fixLen = FIX_STR.Length;
        int pwdLen = pwd.Length;
        int keyLen = KEY_STR.Length;
        int maxLen = Math.Max(fixLen, pwdLen);

        for (int i = 0; i < maxLen; i++)
        {
            int m = 187; // JS中初始值
            int n = 187;

            if (i < fixLen) m = FIX_STR[i];
            if (i < pwdLen) n = pwd[i];

            int xor = m ^ n;
            int index = xor % keyLen;
            result.Append(KEY_STR[index]);
        }

        return result.ToString();
    }

    /// <summary>
    /// MD5哈希（32位小写）
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <returns>MD5哈希结果</returns>
    public static string Md5Hash(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }



    /// <summary>
    /// URL解码（对应JS的decodeURIComponent）
    /// </summary>
    /// <param name="str">待解码字符串</param>
    /// <returns>解码结果</returns>
    public static string UrlDecode(string str)
    {
        return WebUtility.UrlDecode(str);
    }
}


/// <summary>
/// getEncryptInfo返回结果实体
/// </summary>
public class EncryptInfoResponse
{
    [JsonProperty("err_code")]
    public int ErrCode { get; set; } // 0为成功（ENONE）
    [JsonProperty("encrypt_type")]
    public string[] EncryptType { get; set; }
    [JsonProperty("nonce")]
    public string Nonce { get; set; }
    [JsonProperty("key")]
    public string Key { get; set; } // RSA公钥，需decodeURIComponent
}

/// <summary>
/// 登录请求参数实体
/// </summary>
public class LoginRequest
{
    [JsonProperty("method")]
    public string Method => "do";
    [JsonProperty("login")]
    public LoginBody Login { get; set; } = new LoginBody();
}

public class LoginBody
{
    [JsonProperty("password")]
    public string Password { get; set; }
    [JsonProperty("encrypt_type")]
    public string EncryptType { get; set; } // 3或空
}

/// <summary>
/// get_encrypt_info请求参数
/// </summary>
public class EncryptInfoRequest
{
    [JsonProperty("method")]
    public string Method => "do";
    [JsonProperty("user_management")]
    public UserManagement UserManagement { get; set; } = new UserManagement();
}

public class UserManagement
{
    [JsonProperty("get_encrypt_info")]
    public object GetEncryptInfo => null;
}
