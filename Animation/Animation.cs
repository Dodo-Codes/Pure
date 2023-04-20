﻿namespace Pure.Animation;

/// <summary>
/// Represents an animation that can iterate over a sequence of values of type 
/// <typeparamref name="T"/> over time.
/// </summary>
/// <typeparam name="T">The type of the values in the animation.</typeparam>
public class Animation<T>
{
	/// <summary>
	/// Gets the current value of the animation.
	/// </summary>
	public T CurrentValue => values[CurrentIndex];
	/// <summary>
	/// Gets the current index of the animation.
	/// </summary>
	public int CurrentIndex => (int)MathF.Round(RawIndex);
	/// <summary>
	/// Gets or sets the duration of the animation in seconds.
	/// </summary>
	public float Duration { get; set; }
	/// <summary>
	/// Gets or sets the speed of the animation.
	/// </summary>
	public float Speed
	{
		get => Duration / values.Length;
		set => Duration = value * values.Length;
	}
	/// <summary>
	/// Gets or sets a value indicating whether the animation should repeat.
	/// </summary>
	public bool IsRepeating { get; set; }
	/// <summary>
	/// Gets or sets a value indicating whether the animation is paused.
	/// </summary>
	public bool IsPaused { get; set; }
	/// <summary>
	/// Gets or sets the current progress of the animation as a value between 0 and 1.
	/// </summary>
	public float CurrentProgress
	{
		get => Map(rawIndex, LOWER_BOUND, values.Length, 0, 1);
		set => rawIndex = Map(value, 0, 1, LOWER_BOUND, values.Length);
	}

	/// <summary>
	/// Gets or sets the value at the specified index.
	/// </summary>
	/// <param name="index">The index of the value to get or set.</param>
	/// <returns>The value at the specified index.</returns>
	public T this[int index]
	{
		get => values[index];
		set => values[index] = value;
	}

	/// <summary>
	/// Initializes a new instance of the animation with the specified <paramref name="duration"/>, 
	/// repetition, and <paramref name="values"/>.
	/// </summary>
	/// <param name="duration">The duration of the animation in seconds.</param>
	/// <param name="isRepeating">A value indicating whether the animation should repeat from the beginning
	/// after it has finished playing through all the <paramref name="values"/>.</param>
	/// <param name="values">The values of the animation.</param>
	public Animation(float duration, bool isRepeating, params T[] values)
	{
		if (values == null)
			throw new ArgumentNullException(nameof(values));

		this.values = Copy(values);
		rawIndex = 0;
		Duration = duration;
		IsRepeating = isRepeating;
		RawIndex = LOWER_BOUND;
	}
	/// <summary>
	/// Initializes a new instance of the animation with the specified <paramref name="values"/>, 
	/// repeating and <paramref name="speed"/> properties set.
	/// </summary>
	/// <param name="isRepeating">A value indicating whether the animation should repeat 
	/// from the beginning after it has finished playing through all the <paramref name="values"/>.</param>
	/// <param name="speed">The speed at which the animation should play, as <paramref name="values"/>
	/// per second.</param>
	/// <param name="values">The values to be animated.</param>
	public Animation(bool isRepeating, float speed, params T[] values)
		: this(0f, isRepeating, values)
	{
		Speed = speed;
	}
	/// <summary>
	/// Initializes a new instance of the animation with the specified <paramref name="values"/> 
	/// and default properties of <code>Duration = 1f</code> and <code>IsRepeating = false</code>
	/// </summary>
	/// <param name="values">The values to be animated.</param>
	public Animation(params T[] values) : this(1f, false, values) { }

	/// <summary>
	/// Updates the animation progress based on the specified delta time.
	/// </summary>
	/// <param name="deltaTime">The amount of time that has passed since the last update.</param>
	public void Update(float deltaTime)
	{
		if (values == default || IsPaused)
			return;

		RawIndex += deltaTime / Speed;
		if ((int)MathF.Round(RawIndex) >= values.Length)
			RawIndex = IsRepeating ? LOWER_BOUND : values.Length - 1;
	}

	/// <summary>
	/// Implicitly converts an array of values to an Animation object.
	/// </summary>
	/// <param name="values">The values to be animated.</param>
	public static implicit operator Animation<T>(T[] values) => new(values);
	/// <summary>
	/// Implicitly converts an Animation object to an array of values.
	/// </summary>
	/// <param name="animation">The Animation object to convert.</param>
	public static implicit operator T[](Animation<T> animation) => Copy(animation.values);

	/// <returns>
	/// An array copy containing the values of type <typeparamref name="T"/> 
	/// in the animation sequence.</returns>
	public T[] ToArray() => this;

	#region Backend
	private readonly T[] values;

	private float rawIndex;
	private const float LOWER_BOUND = -0.49f;

	private float RawIndex
	{
		get => rawIndex;
		set => rawIndex = Math.Clamp(value, LOWER_BOUND, values.Length);
	}

	private static float Map(float number, float a1, float a2, float b1, float b2)
	{
		var value = (number - a1) / (a2 - a1) * (b2 - b1) + b1;
		return float.IsNaN(value) || float.IsInfinity(value) ? b1 : value;
	}
	private static int Wrap(int number, int targetNumber)
	{
		return ((number % targetNumber) + targetNumber) % targetNumber;
	}
	private static T[] Copy(T[] array)
	{
		var copy = new T[array.Length];
		Array.Copy(array, copy, array.Length);
		return copy;
	}
	#endregion
}