using UnityEngine;
using UnityEditor;

/// <summary>
/// 打开 TextMeshPro Font Asset Creator 的快捷方式
/// </summary>
public class CreateChineseFontAsset : EditorWindow
{
    private Font sourceFont;

    [MenuItem("Tools/Create Chinese Font Asset")]
    public static void ShowWindow()
    {
        GetWindow<CreateChineseFontAsset>("创建中文字体");
    }

    void OnGUI()
    {
        GUILayout.Label("创建 TextMeshPro 中文字体", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "这个工具会帮你打开 TextMeshPro 的字体生成器，\n" +
            "并提供正确的配置参数。",
            MessageType.Info
        );

        EditorGUILayout.Space();

        sourceFont = (Font)EditorGUILayout.ObjectField("1. 源字体文件", sourceFont, typeof(Font), false);

        EditorGUILayout.Space();

        if (sourceFont == null)
        {
            EditorGUILayout.HelpBox(
                "请从 Project 窗口拖拽 simsunb 到上面的框中！\n\n" +
                "位置：Assets/Fonts/simsunb",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space();
        GUILayout.Label("2. 配置参数（复制下面的值）", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "字符序列（Unicode 十六进制）：\n\n" +
            "0020-007E,4E00-62FF,6300-77FF,7800-8CFF,8D00-9FA5,3000-303F,FF00-FFEF\n\n" +
            "（这包含了常用汉字、标点和英文字符）",
            MessageType.None
        );

        if (GUILayout.Button("复制字符序列到剪贴板"))
        {
            GUIUtility.systemCopyBuffer = "0020-007E,4E00-62FF,6300-77FF,7800-8CFF,8D00-9FA5,3000-303F,FF00-FFEF";
            Debug.Log("✅ 字符序列已复制到剪贴板");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GUILayout.Label("推荐设置：", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("• Sampling Point Size: 42");
        EditorGUILayout.LabelField("• Padding: 5");
        EditorGUILayout.LabelField("• Atlas Resolution: 2048 x 2048");
        EditorGUILayout.LabelField("• Character Set: Unicode Range (Hex)");

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GUI.enabled = sourceFont != null;

        if (GUILayout.Button("3. 打开 Font Asset Creator", GUILayout.Height(50)))
        {
            OpenFontAssetCreator();
        }

        GUI.enabled = true;

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "步骤说明：\n\n" +
            "1. 拖拽 simsunb 字体到上面的框\n" +
            "2. 点击 '复制字符序列' 按钮\n" +
            "3. 点击 '打开 Font Asset Creator'\n" +
            "4. 在打开的窗口中：\n" +
            "   - Source Font File: 会自动填充 simsunb\n" +
            "   - Character Set: 选择 'Unicode Range (Hex)'\n" +
            "   - Character Sequence: 粘贴复制的字符序列\n" +
            "   - 点击 'Generate Font Atlas'\n" +
            "   - 等待生成完成（2-5分钟）\n" +
            "   - 点击 'Save' 保存到 Assets/Fonts/SimSunB_SDF",
            MessageType.Info
        );
    }

    void OpenFontAssetCreator()
    {
        if (sourceFont == null)
        {
            EditorUtility.DisplayDialog("提示", "请先拖拽字体文件到 '源字体文件' 框中！", "确定");
            return;
        }

        // 打开 Font Asset Creator
        EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Font Asset Creator");

        Debug.Log("=== TextMeshPro Font Asset Creator 配置参数 ===");
        Debug.Log($"Source Font: {sourceFont.name}");
        Debug.Log("Sampling Point Size: 42");
        Debug.Log("Padding: 5");
        Debug.Log("Atlas Resolution: 2048 x 2048");
        Debug.Log("Character Set: Unicode Range (Hex)");
        Debug.Log("Character Sequence: 0020-007E,4E00-62FF,6300-77FF,7800-8CFF,8D00-9FA5,3000-303F,FF00-FFEF");
        Debug.Log("===============================================");

        EditorUtility.DisplayDialog(
            "下一步",
            "Font Asset Creator 已打开！\n\n" +
            "请按照以下步骤操作：\n\n" +
            "1. Source Font File: 拖拽 simsunb\n" +
            "2. Sampling Point Size: 42\n" +
            "3. Padding: 5\n" +
            "4. Atlas Resolution: 2048 x 2048\n" +
            "5. Character Set: 选择 'Unicode Range (Hex)'\n" +
            "6. Character Sequence: 粘贴复制的字符序列\n" +
            "   (已复制到剪贴板，直接 Ctrl+V)\n" +
            "7. 点击 'Generate Font Atlas'\n" +
            "8. 等待完成后点击 'Save'\n" +
            "9. 保存为: Assets/Fonts/SimSunB_SDF",
            "开始操作"
        );
    }
}
