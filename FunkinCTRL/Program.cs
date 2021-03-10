/* 
 * Created by SharpDevelop.
 * Date: 06.03.2021
 * Time: 17:28
 * 
 * (C) 2021 redthedev
 * 
 * This project uses following side libraries:
 * + WindowsInput: for emulating key pressing (https://github.com/michaelnoonan/inputsimulator) [MIT License]
 * + Lego Mindstorms EV3 API wrapper (https://github.com/BrianPeek/legoev3) [Apache 2.0 License]
 * 
 * Presented source code is free for any modifications, as long as you credit the original author.
 */
using System;
using System.Runtime.InteropServices;
using System.Threading;

using Lego.Ev3.Core;
using Lego.Ev3.Desktop;

using WindowsInput;
using WindowsInput.Native;

namespace FunkinCTRL
{
	/// <summary>
	/// Object that serves as button switch.
	/// </summary>
	class ClickableButton
	{
		VirtualKeyCode _tk;
		/// <summary>
		/// Key that will be triggered upon mutating IsDown to true.
		/// </summary>
		public VirtualKeyCode TriggeredKey
		{
			get
			{
				return _tk;
			}
			set
			{
				IsDown = false;
				_tk = value;
			}
		}
		Boolean _down;
		readonly InputSimulator _is;
		/// <summary>
		/// On mutated, will press key either down or up.
		/// </summary>
		public Boolean IsDown
		{
			get
			{
				return _down;
			}
			set
			{
				if (_down && !value)
				{
					_is.Keyboard.KeyUp(TriggeredKey);
				}
				else if (!_down && value)
				{
					_is.Keyboard.KeyDown(TriggeredKey);
				}
				_down = value;
			}
		}
		
		public ClickableButton(VirtualKeyCode triggeredKey, InputSimulator target)
		{
			_is = target;
			_tk = triggeredKey;
		}
	}
	
	/// <summary>
	/// Main runtime scope.
	/// </summary>
	class Program
	{
		const Int32 RequestFrequency = 1000;
		static InputSimulator _is = new InputSimulator();
		static Brick b;
		
		static ClickableButton EnterKey = new ClickableButton(VirtualKeyCode.RETURN, _is);
		static ClickableButton EscapeKey = new ClickableButton(VirtualKeyCode.ESCAPE, _is);
		
		/// <summary>
		/// Directional keys.
		/// </summary>
		static ClickableButton[] _buttons = {new ClickableButton(VirtualKeyCode.RIGHT, _is), new ClickableButton(VirtualKeyCode.UP, _is), new ClickableButton(VirtualKeyCode.DOWN, _is), new ClickableButton(VirtualKeyCode.LEFT, _is)};
		
		/// <summary>
		/// Will press needed key depending on Touch sensor and IR sensor data.
		/// </summary>
		/// <param name="Touch">Data from Touch sensor (Port one)</param>
		/// <param name="Prox">Data from IR Proximity sensor (Port four)</param>
		public static void ParseControllerInputs(Single Touch, Single Prox)
		{
			Boolean IsTriggered = Touch == 0.0f;
			if (Prox > 60)
			{
				Prox = 12;
			}
			Prox -= 12;
			
			if (!IsTriggered)
			{
				for (int i = 0; i < 4; i++)
				{
					_buttons[i].IsDown = false;
				}
				return;
			}
			
			Int32 KeySelection = (((Int32) Math.Round(Prox)) / 12);
			
			if (KeySelection < 0)
			{
				for (int i = 0; i < 4; i++)
				{
					_buttons[i].IsDown = false;
				}
				return;
			}
			
			for (int i = 0; i < 4; i++)
			{
				if (i == KeySelection)
				{
					_buttons[i].IsDown = true;
				}
				else
				{
					_buttons[i].IsDown = false;
				}
			}
		}
		
		/// <summary>
		/// Entry point.
		/// </summary>
		/// <param name="args">Command line args.</param>
		public static void Main(string[] args)
		{
			Console.WriteLine("Initializing FunkinCTRL...");
			ICommunication ic;
			
			Console.WriteLine("Select communication method:\n-- For USB, press 0\n-- For Bluetooth, press 1");
			switch (Console.ReadKey(true).KeyChar)
			{
				case '1':
					// TODO: Find a better implementation.
					Console.WriteLine("Enter the COM-port data.");
					ic = new BluetoothCommunication(Console.ReadLine());
					break;
				case '0':
					ic = new UsbCommunication();
					break;
				default:
					Console.WriteLine("Unrecognized input, selecting USB communication.");
					ic = new UsbCommunication();
					break;
			}
			Console.WriteLine("Connecting to EV3 controller...");
			b = new Brick(ic, true);
			TimeSpan ts = new TimeSpan(TimeSpan.TicksPerSecond / RequestFrequency);
			b.ConnectAsync(ts);
			
			Thread.Sleep(2000);
			
			b.BatchCommand.PlayTone(5, 440, 300);
			b.BatchCommand.SendCommandAsync();
			
			b.ConnectAsync(ts);
			Thread.Sleep(1000);
			
			if (b.Ports[InputPort.D].Type != DeviceType.MMotor || b.Ports[InputPort.One].Type != DeviceType.Touch || b.Ports[InputPort.Four].Type != DeviceType.Infrared)
			{
				Console.WriteLine("Unable to connect to controller or controller layout is incorrect.");
				Console.ReadKey(true);
				Environment.Exit(-1);
			}
			
			Console.WriteLine("Setup complete. Press center button on your controller to continue.");
			while (!b.Buttons.Enter) {}
			
			while (!b.Buttons.Down)
			{
				Single touch = b.Ports[InputPort.One].SIValue;
				Single ir = b.Ports[InputPort.Four].SIValue;
				Single motor = b.Ports[InputPort.D].SIValue;
				
				String title = String.Format("FunkinCTRL -- Controls engaged // D -- {0}:{1}, #1 -- {2}:{3}, #4 -- {4}:{5} // Press DOWN button to exit loop.", b.Ports[InputPort.D].Type, motor, b.Ports[InputPort.One].Type, touch, b.Ports[InputPort.Four].Type, ir);
				Console.Title = title;
				
				ParseControllerInputs(touch, ir);
				EscapeKey.IsDown = b.Buttons.Up;
				EnterKey.IsDown = b.Buttons.Enter;
			}
			
			b.Disconnect();
			
			Console.Error.WriteLine("Program ended.");
			
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
	}
}