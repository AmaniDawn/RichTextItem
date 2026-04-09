using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// RichTextItem 超链接和表情功能测试示例
    /// </summary>
    public class RichTextLinkTest : MonoBehaviour
    {
        [SerializeField] private RichTextItem m_richText;

        private void Start()
        {
            if (m_richText == null)
            {
                m_richText = GetComponent<RichTextItem>();
            }

            if (m_richText == null)
            {
                Debug.LogError("RichTextLinkTest: 未找到 RichTextItem 组件");
                return;
            }

            // 注册表情动画
            RegisterEmojis();

            // 设置链接点击回调
            m_richText.OnLinkClicked = OnLinkClicked;

            // 显示测试内容
            ShowTestContent();
        }

        /// <summary>
        /// 注册表情动画帧
        /// </summary>
        private void RegisterEmojis()
        {
            // 卖萌表情 [maimeng] - 2帧
            RichTextConfig.RegisterEmoji("[maimeng]", "tongyong01_maimeng01", 1);
            RichTextConfig.RegisterEmoji("[maimeng]", "tongyong01_maimeng02", 2);

            // 开心表情 [kaixin] - 4帧
            RichTextConfig.RegisterEmoji("[kaixin]", "tongyong22_kaixin01", 1);
            RichTextConfig.RegisterEmoji("[kaixin]", "tongyong22_kaixin02", 2);
            RichTextConfig.RegisterEmoji("[kaixin]", "tongyong22_kaixin03", 3);
            RichTextConfig.RegisterEmoji("[kaixin]", "tongyong22_kaixin04", 4);

            // 鼓掌表情 [guzhang] - 3帧
            RichTextConfig.RegisterEmoji("[guzhang]", "tongyong18_guzhang01", 1);
            RichTextConfig.RegisterEmoji("[guzhang]", "tongyong18_guzhang02", 2);
            RichTextConfig.RegisterEmoji("[guzhang]", "tongyong18_guzhang03", 3);

            // 哭表情 [ku] - 2帧
            RichTextConfig.RegisterEmoji("[ku]", "tongyong21_ku01", 1);
            RichTextConfig.RegisterEmoji("[ku]", "tongyong21_ku02", 2);

            // 流汗表情 [liuhan] - 3帧
            RichTextConfig.RegisterEmoji("[liuhan]", "tongyong20_liuhan01", 1);
            RichTextConfig.RegisterEmoji("[liuhan]", "tongyong20_liuhan02", 2);
            RichTextConfig.RegisterEmoji("[liuhan]", "tongyong20_liuhan03", 3);

            // 尴尬表情 [ganga] - 3帧
            RichTextConfig.RegisterEmoji("[ganga]", "tongyong72_ganga01", 1);
            RichTextConfig.RegisterEmoji("[ganga]", "tongyong72_ganga02", 2);
            RichTextConfig.RegisterEmoji("[ganga]", "tongyong72_ganga03", 3);

            // 大爱表情 [daai] - 2帧
            RichTextConfig.RegisterEmoji("[daai]", "tongyong62_daai01", 1);
            RichTextConfig.RegisterEmoji("[daai]", "tongyong62_daai02", 2);
        }

        private void ShowTestContent()
        {
            // 测试文本 - 包含多种链接格式
            // string content =
            //     "欢迎来到游戏!\n" +
            //     "[icon:Play_Joystick_bg]x100[link:1001|查看公告|#00BFFF|underline]\n" +
            //     "[link:1002|领取奖励|#FFD700|underline]\n" +
            //     "[link:1003|联系客服|#FF6347]\n" +
            //     "普通文本 [link:1004|点击这里] 继续阅读";
            //
            // m_richText.SetText(content);
            TestAllFeatures();
        }

        private void OnLinkClicked(LinkData data)
        {
            Debug.Log($"链接点击: ID={data.LinkID}, Text={data.LinkText}, Color={data.LinkColor}, Style={data.Style}");

            switch (data.LinkID)
            {
                case 1001:
                    Debug.Log("→ 打开公告界面");
                    break;
                case 1002:
                    Debug.Log("→ 打开领奖界面");
                    break;
                case 1003:
                    Debug.Log("→ 打开客服界面");
                    break;
                case 1004:
                    Debug.Log("→ 继续阅读");
                    break;
                default:
                    Debug.Log($"→ 未处理的链接ID: {data.LinkID}");
                    break;
            }
        }

        /// <summary>
        /// 测试不同的链接格式
        /// </summary>
        [ContextMenu("测试基础链接")]
        public void TestBasicLink()
        {
            m_richText.OnLinkClicked = OnLinkClicked;
            m_richText.SetText("点击[link:1|这里]查看详情");
        }

        [ContextMenu("测试带颜色链接")]
        public void TestColorLink()
        {
            m_richText.OnLinkClicked = OnLinkClicked;
            m_richText.SetText("访问[link:2|官网|#FFD700]获取更多信息");
        }

        [ContextMenu("测试带下划线链接")]
        public void TestUnderlineLink()
        {
            m_richText.OnLinkClicked = OnLinkClicked;
            m_richText.SetText("[link:3|立即领取|#FF0000|underline]奖励!");
        }

        [ContextMenu("测试混合内容")]
        public void TestMixedContent()
        {
            m_richText.OnLinkClicked = OnLinkClicked;
            m_richText.SetText(
                "<color=#FFFFFF>恭喜!</color>\n" +
                "你获得了[icon:tongyong02_qinqin02]x<color=#FF0000>100</color>\n" +
                "[link:100|领取奖励|#00FF00|underline]");
        }

        [ContextMenu("测试表情")]
        public void TestEmoji()
        {
            m_richText.OnLinkClicked = OnLinkClicked;
            m_richText.SetText(
                "表情测试:\n" +
                "卖萌[maimeng] 开心[kaixin] 鼓掌[guzhang]\n" +
                "哭泣[ku] 流汗[liuhan] 尴尬[ganga] 大爱[daai]");
        }

        [ContextMenu("测试表情+链接+图标")]
        public void TestAllFeatures()
        {
            m_richText.OnLinkClicked = OnLinkClicked;
            m_richText.SetText(
                "<color=#FFD700>恭喜您获得奖励!</color>[kaixin]\n" +
                "获得金币[icon:tongyong02_qinqin02]x<color=#FF0000>999</color>\n" +
                "[guzhang]太棒了![guzhang]\n" +
                "[link:1|立即领取|#00FF00|underline] [link:2|分享好友|#00BFFF|underline]");
        }
    }
}