namespace WebAPI.Common;

public class ApiResult
{
    public int Code { get; set; }

    public string Message { get; set; } = "success";

    public object? Data { get; set; }

    public static ApiResult Success(object? data = null, string message = "success")
    {
        return new ApiResult
        {
            Code = 0,
            Message = message,
            Data = data
        };
    }

    public static ApiResult Fail(string message = "fail", int code = -1, object? data = null)
    {
        return new ApiResult
        {
            Code = code,
            Message = message,
            Data = data
        };
    }
    public class ApiResponse<T>
    {
        public int Code { get; set; }
        public string Message { get; set; } = "ok";
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T? data = default) => new ApiResponse<T> { Code = 0, Message = "ok", Data = data };
        public static ApiResponse<T> Fail(string message, int code = -1) => new ApiResponse<T> { Code = code, Message = message, Data = default };
    }
}
