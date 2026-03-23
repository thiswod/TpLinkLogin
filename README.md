# TpLinkLogin

一个用于生成 TP-Link 路由器 Web 管理后台登录请求参数（加密密码字段）的 .NET 控制台项目。

项目会先向路由器发送 `get_encrypt_info` 请求获取 `nonce / encrypt_type` 等信息，然后根据返回的加密类型对原始密码进行处理，最终输出可直接用于登录请求的 JSON。

## 功能

- 调用路由器接口获取加密信息（`get_encrypt_info`）
- 根据 `encrypt_type` 自动选择加密方式
  - 包含 `"3"`：`password + ":" + nonce` → MD5(32位小写)
  - 否则：使用项目内置的对称混淆算法生成密码字段
- 输出登录请求 JSON（`method=do`，包含 `login.password` 与可选的 `login.encrypt_type`）

## 环境要求

- .NET SDK：`net10.0`（见 [TpLinkLogin.csproj](TpLinkLogin.csproj)）

## 快速开始

1. 修改 [Program.cs](Program.cs) 中的路由器 IP 与密码（示例目前为硬编码）：

   - `var routerIp = "192.168.0.1";`
   - `string rawPwd = "YourPassword";`

2. 运行：

   ```powershell
   dotnet restore
   dotnet run -c Release
   ```

3. 控制台会打印“加密后的登录请求参数”，形如：

   ```json
   {"method":"do","login":{"password":"...","encrypt_type":"3"}}
   ```

4. 当前示例代码会把该 JSON POST 到 `http://<router-ip>/`，但不会解析/展示登录响应，也不会自动提取 cookie/token。

## 依赖版本

- Newtonsoft.Json：`13.0.4`
- WodToolKit：`1.0.2.6`

## 与路由器交互说明

项目内部实际做了两步：

1. 获取加密信息（POST 到路由器根路径 `/`）：

   - URL：`http://<router-ip>/`
   - Content-Type：`application/json`
   - 请求体：

     ```json
     {"method":"do","user_management":{"get_encrypt_info":null}}
     ```

2. 生成登录请求 JSON（同样 POST 到 `http://<router-ip>/`）。

注意：当前项目示例只负责发送请求，不会自动提取/保存登录态（cookie/token）。如需完整登录闭环，需要补充响应解析与 cookie 管理。

## 代码结构

- [Program.cs](Program.cs)
  - `TplinkLoginHelper`：封装获取加密信息与构建登录请求
  - `TplinkEncryptUtil`：MD5 与自定义对称混淆逻辑
  - 请求/响应模型：`EncryptInfoRequest / EncryptInfoResponse / LoginRequest` 等

## 注意事项

- 请仅在你拥有管理权限的设备上使用。
- 示例代码把密码写在源码里，仅用于演示；实际使用建议避免硬编码（例如改为环境变量或交互输入）。
- 项目请求使用 `http://`，并通过 WodToolKit 的 `HttpRequestClass` 发起请求；如需长期维护，建议评估迁移到 `HttpClient` 并完善超时/重试/异常处理。

## 贡献

欢迎提交 Issue / PR（例如：完善登录响应解析、cookie 保存、错误输出、参数化配置等）。提交前建议先运行：

```powershell
dotnet build -c Release
```

## 许可证

见 [LICENSE.txt](LICENSE.txt)。
