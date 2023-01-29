using System;
using System.Collections;
using UnityEngine;
using Meteor.Extensions;
using Meteor.Internal;

namespace Meteor
{
	/// <summary>
	/// Methods are remote functions that Meteor clients can invoke.
	/// Use <see cref="Method.Call"/> to call a method by name with the specified arguments. Returns a Method object.
	/// You must call <code>yield return (Coroutine)instance; </code> inside an IEnumerator/Coroutine function to actually execute the call. You can also call <code>ExecuteAsync</code> on the
	/// method instance to execute asynchronously outside of an IEnumerator.
	/// The name of the method to call corresponds to the key in your <code>Meteor.Methods({key: function value()})</code> statement.
	/// </summary>
	/// <example>
	/// Examples:
	/// <code>
	/// var methodCall = Method<string>.Call("MyMethodWhichREturnsString");
	/// yield return (Coroutine)methodCall;
	/// string result = methodCall.Result;
	/// </code>
	/// </example>
	/// <example>
	/// <code>
	/// var callAndForgetAboutResult = Method<string>.Call("MyMethodWhichReturnsString").ExecuteAsync();
	/// </code>
	/// </example>
	public class Method : Meteor.Internal.IMethod
	{
		public Method ()
		{
			Updated = false;
		}

		internal MethodMessage Message;

		protected event MethodHandler OnUntypedResponse;

		/// <summary>
		/// The name of the method called. Corresponds to the key in your Meteor.Methods({key: function value()}) statement.
		/// </summary>
		/// <value>String name.</value>
		public string Name {
			get;
			protected set;
		}

		/// <summary>
		/// Calls a method by name with the specified arguments. Returns a Method object.
		/// You must call <code>yield return (Coroutine)instance; </code> inside an IEnumerator/Coroutine function to actually execute the call. You can also call <code>ExecuteAsync</code> on the
		/// method instance to execute asynchronously outside of an IEnumerator.
		/// </summary>
		/// <example>
		/// Examples:
		/// <code>
		/// var methodCall = Method.Call("myMethod");
		/// yield return (Coroutine)methodCall;
		/// var result = methodCall.Result;
		/// </code>
		/// </example>
		/// <example>
		/// <code>
		/// var callAndForgetAboutResult = Method.Call("myMethod").ExecuteAsync();
		/// </code>
		/// </example>
		/// <param name="name">Name of the method to call. Corresponds to the key in your Meteor.Methods({key: function value()}) statement.</param>
		/// <param name="args">Arguments. This can be an array of any typed objects. They will be faithfully converted to Meteor, including e.g. Vector3's. It is recommended to use
		/// a single argument with a typed class in C# instead of multiple primitive arguments.</param>
		public static Method Call (string name, params object[] args)
		{
			var methodCall = LiveData.Instance.Call (name, args);
			methodCall.Name = name;
			return methodCall;
		}

		/// <summary>
		/// Used by LiveData to signal that a response has arrived for this method call.
		/// </summary>
		/// <param name="error">Error.</param>
		/// <param name="response">Response.</param>
		public virtual void Callback (Error error, object response)
		{
			if (OnUntypedResponse != null) {
				OnUntypedResponse (error, response);
			}
		}

		/// <summary>
		/// Gets the type of the response. Returns <code>typeof(IDictionary)</code> for untyped method calls.
		/// </summary>
		/// <value>The type of the response.</value>
		public virtual Type ResponseType {
			get {
				return typeof(IDictionary);
			}
		}

		#region IMethod implementation

		/// <summary>
		/// Gets the untyped response of this method call. For typed method calls, <see cref="Meteor.Method`1"/>
		/// </summary>
		/// <value>The untyped response.</value>
		public object UntypedResponse {
			get;
			protected set;
		}

		/// <summary>
		/// The Meteor error of this method call.
		/// </summary>
		/// <value>The error.</value>
		public Error Error {
			get;
			protected set;
		}

		/// <summary>
		/// Gets a value indicating whether all of the database side effects of this method call have been synchronized.
		/// </summary>
		/// <value><c>true</c> if updated; otherwise, <c>false</c>.</value>
		public bool Updated {
			get;
			set;
		}

		#endregion

		protected bool complete;

		protected void completed (Error error, object response)
		{
			UntypedResponse = response;
			Error = error;
			complete = true;
		}

		protected virtual IEnumerator Execute ()
		{
			// Send the method message over the wire.
			while (!Connection.Connected
				&& !LiveData.Instance.TimedOut) {
				yield return null;
			}

			if (LiveData.Instance.TimedOut) {
				Callback (new Error () { error = -1, details = "Connection timed out." }, null);
				yield break;
			}

			// Send the method message over the wire.
			LiveData.Instance.Send (Message);

			// Wait until we get a response.
			while (!(complete && Updated)) {
				yield return null;
			}

			// Clear the completed handler.
			OnUntypedResponse -= completed;

			yield break;
		}

		/// <summary>
		/// Executes the method immediately, using a coroutine host global to your Unity game.
		/// </summary>
		/// <returns>The async.</returns>
		/// <param name="callback">An optional callback when this method returns (which may never happen).</param>
		public virtual Coroutine ExecuteAsync (MethodHandler callback = null)
		{
			this.OnUntypedResponse += callback;
			return (Coroutine)this;
		}

		/// <summary>
		/// Casts this method call instance to a Coroutine, allowing you to yield and execute it.
		/// </summary>
		public static implicit operator Coroutine (Method method)
		{
			if (method == null) {
				return null;
			}
			method.OnUntypedResponse += method.completed;
			return CoroutineHost.Instance.StartCoroutine (method.Execute ());
		}
	}
}

