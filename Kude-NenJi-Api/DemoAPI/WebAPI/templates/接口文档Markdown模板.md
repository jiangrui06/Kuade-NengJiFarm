# 接口文档 Markdown 模板

> 模板用途：后续新增接口时，直接复制本模板并替换字段内容  
> 模板位置：`C:\Users\Administrator\Desktop\WebAPI\templates\接口文档Markdown模板.md`

## 模块名称

<details>
<summary><strong>请求方法 接口路径</strong> - 接口名称</summary>

### 中文注释
- 这里写接口的业务用途。
- 这里写调用时机。
- 这里写联调注意事项。

### 是否鉴权
- 是 / 否

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | int | 是 | 示例字段 |

### 请求示例

```json
{
  "id": 1
}
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {}
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 404 | 资源不存在 |

</details>
