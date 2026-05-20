namespace WebAPI.Dtos
{
    /// <summary>
    /// �û��б����ӦDTO������API�����ݷ���
    /// </summary>
    public class UserListItemDto
    {
        /// <summary>
        /// �û�ΨһID����ʽΪ��U + yyyyMMdd + ��ţ����磺U20260101120019
        /// </summary>
        public string id { get; set; } = null!;

        public string Guid { get; set; } = null!;

        public string phone { get; set; } = null!;
        public string nickname { get; set; } = null!;
        public string? gender { get; set; }
        public string? address { get; set; }

        public string? WxOpenid { get; set; }

        /// <summary>
        /// ��ɫ��������/��ͨ�û�
        /// </summary>
        public string role { get; set; } = null!;

        /// <summary>
        /// �Ƿ�ѡ��
        /// </summary>
        public bool selected { get; set; } = false;

        /// <summary>
        /// �û����� - ����ƽ̨�Ĺ�����Ա(staff)����ƽ̨���΢���û�(user)
        /// </summary>
        public string userType { get; set; } = null!;
        public string? loginTime { get; set; }
    }

    /// <summary>
    /// �û��б���ҳ��ӦDTO
    /// </summary>
    public class UserListPageDto
    {
        /// <summary>
        /// ��ǰҳ�루��1��ʼ��
        /// </summary>
        public int pageNum { get; set; }

        /// <summary>
        /// ÿҳ��¼��
        /// </summary>
        public int pageSize { get; set; }

        /// <summary>
        /// �ܼ�¼��
        /// </summary>
        public int total { get; set; }

        /// <summary>
        /// ��ҳ��
        /// </summary>
        public int pages { get; set; }

        /// <summary>
        /// �û��б�����
        /// </summary>
        public List<UserListItemDto> records { get; set; } = new();
    }

    /// <summary>
    /// �����û�����DTO
    /// </summary>
    public class AddUserDto
    {
        public string Phone { get; set; } = null!;
        public string RealName { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public int? RoleId { get; set; } = null!;

        public string Password { get; set; } = null!;
    }

    public class UserDetailDto
    {
        public string Guid { get; set; } = "";
        public string phone { get; set; } = "";
        public string nickname { get; set; } = "";
        public string avatar { get; set; } = "";
        public string gender { get; set; } = "";
        public string loginTime { get; set; } = "";
        public int id { get; set; }
        public string realName { get; set; } = "";
        public string? wxOpenId { get; set; }
        public int roleId { get; set; }
        public string roleName { get; set; } = "";
    }

    /// <summary>
    /// �༭�û�����DTO
    /// �û���ǰ�˿�ѡ��ĳ���ֶ��޸ģ������ͱ�����ֶΣ������ֶα��ֲ���
    /// </summary>
    public class EditUserDto
    {
        /// <summary>
        /// �û�ID�������ṩ���Դ�ʶ��Ҫ�༭�ĸ��û�
        /// </summary>
        public string Guid { get; set; } = null!;

        /// <summary>
        /// �ǳƣ���ѡ�����޸�ʱ�ŷ���
        /// </summary>
        public string? nickname { get; set; }

        /// <summary>
        /// �Ա𣬿�ѡ�����޸�ʱ�ŷ���
        /// </summary>
        public string? gender { get; set; }

        /// <summary>
        /// ��ַ����ѡ�����޸�ʱ�ŷ���
        /// </summary>
        //public string? address { get; set; }

        /// <summary>
        /// ��ɫ�������ǣ�����Ա/��ͨ�û�����ѡ�����޸�ʱ�ŷ���
        /// </summary>
        public string? role { get; set; }

        /// <summary>
        /// ״̬�������ǣ�����/���ã���ѡ�����޸�ʱ�ŷ���
        /// </summary>
        //public string? status { get; set; }
    }

    /// <summary>
    /// �޸��û�״̬����DTO
    /// </summary>
    public class ChangeStatusDto
    {
        /// <summary>
        /// �û�ID
        /// </summary>
        public string id { get; set; } = null!;

        /// <summary>
        /// Ŀ��״̬�������ǣ�����/����
        /// </summary>
        public string status { get; set; } = null!;
    }

    /// <summary>
    /// ɾ���û�����DTO
    /// </summary>
    public class DeleteUserDto
    {
        public string Guid { get; set; } = null!;
    }

    /// <summary>
    /// �û���¼����DTO
    /// </summary>
    public class LoginDto
    {
        public string user_no { get; set; } = null!;
        public string password { get; set; } = null!;
    }

    /// <summary>
    /// �û���¼��ӦDTO
    /// </summary>
    public class LoginResponseDto
    {
        public string user_no { get; set; } = null!;

        public string role { get; } = "user";

        public string LoginTime { get; set; } = DateTime.Now.ToString("yyyy��MM��dd�� HH:mm");

        public string token { get; set; } = null!;

        public string user_password { get; set; } = null!;
    }
}