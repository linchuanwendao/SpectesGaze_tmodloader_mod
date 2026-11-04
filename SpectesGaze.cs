using Terraria.ModLoader;

namespace SpectesGaze
{
	// 模组主类。全局的东西（按键、配置）放这里，业务逻辑不写在这。
	public class SpectesGaze : Mod
	{
		// 开 / 关鬼域。默认不绑定，玩家自己去 设置 -> 按键绑定 里绑。
		public static ModKeybind GazeKey { get; private set; }

		// 主动大激光。第五层起可用，同样默认不绑定。
		public static ModKeybind BeamKey { get; private set; }

		public override void Load()
		{
			GazeKey = KeybindLoader.RegisterKeybind(this, "OpenDomain", "None");
			BeamKey = KeybindLoader.RegisterKeybind(this, "FireBeam", "None");
		}

		public override void Unload()
		{
			// 必须置空，否则重载模组时静态字段会残留旧实例。
			GazeKey = null;
			BeamKey = null;
		}
	}
}
