using Terraria.ModLoader;

namespace SpectesGaze
{
	// 模组主类。全局的东西（按键、配置）放这里，业务逻辑不写在这。
	public class SpectesGaze : Mod
	{
		// 鬼眼的主动技能按键。
		// 默认值是 "None"，即开局不绑定，玩家自己去 设置 -> 按键绑定 里绑。
		public static ModKeybind GazeKey { get; private set; }

		public override void Load()
		{
			GazeKey = KeybindLoader.RegisterKeybind(this, "OpenDomain", "None");
		}

		public override void Unload()
		{
			// 必须置空，否则重载模组时静态字段会残留旧实例。
			GazeKey = null;
		}
	}
}
