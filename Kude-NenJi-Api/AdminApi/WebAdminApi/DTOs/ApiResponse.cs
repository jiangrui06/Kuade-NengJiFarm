namespace WebAdminApi.DTOs
{
    public class ApiResponse<T>
    {
        public int Code { get; set; } = 200;
        public string Message { get; set; } = "请求成功";
        public T? Data { get; set; }

        public static ApiResponse<T> Success(T? data = default, string message = "请求成功")
        {
            return new ApiResponse<T> { Code = 200, Message = message, Data = data };
        }

        public static ApiResponse<T> Error(int code, string message)
        {
            return new ApiResponse<T> { Code = code, Message = message };
        }
    }

    public class ApiResponse
    {
        public int Code { get; set; } = 200;
        public string Message { get; set; } = "请求成功";
        public object? Data { get; set; }
    }
}