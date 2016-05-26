//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AssemblyCSharp
{
	public class Lego
	{
		private ColorSpecification color;
		private Vector3 position, gpos;
		private Vector2 dimen;
		private LegoType type;
		public List<Vector3> gposList;
		public List<Vector3> up;
		public List<Vector3> down;
		public List<Vector3> left;
		public List<Vector3> right;
		public List<Vector3> front;
		public List<Vector3> back;
		public bool isBound = false;
		public bool isVisted = false;

		public Lego (ColorSpecification color, Vector3 position, LegoType type, bool isBound){
			this.color = color;
			this.position = position;
			this.type = type;
			this.isBound = isBound;
			this.dimen = new Vector2(1,1);
			gposList = new List<Vector3> ();
			up = new List<Vector3>();
			down = new List<Vector3>();
			left = new List<Vector3>();
			right = new List<Vector3>();
			front = new List<Vector3>();
			back = new List<Vector3>();
		}

		public void setPosition(Vector3 position){
			this.position = position;
		}

		public void setGPos(Vector3 gpos){
			this.gpos = gpos;
		}

		public void addConnectedLego(List<Vector3> up, List<Vector3> down){
			this.up.Clear ();
			this.down.Clear ();
			this.up.AddRange (up);
			this.down.AddRange (down);
		}

		public void addNeighborLego(List<Vector3> up, List<Vector3> down, List<Vector3> left, List<Vector3> right, List<Vector3> front, List<Vector3> back){
			this.up.Clear ();
			this.down.Clear ();
			this.left.Clear ();
			this.right.Clear ();
			this.front.Clear ();
			this.back.Clear ();

			this.up.AddRange (up);
			this.down.AddRange (down);
			this.left.AddRange (left);
			this.right.AddRange (right);
			this.front.AddRange (front);
			this.back.AddRange (back);
		}

		public void addNeighborLego(List<Vector3> up, List<Vector3> down, List<Vector3> left, List<Vector3> right){
			this.up.Clear ();
			this.down.Clear ();
			this.left.Clear ();
			this.right.Clear ();

			this.up.AddRange (up);
			this.down.AddRange (down);
			this.left.AddRange (left);
			this.right.AddRange (right);
		}

		public void changeType(LegoType type){
			this.type = type;
		}

		public void changePosition(Vector3 position){
			this.position = position;
		}

		public void changeDimen(Vector2 dimen){
			this.dimen = dimen;
		}

		public void changeColor(ColorSpecification color){
			this.color = color;
		}

		public Vector3 getGPosition(){
			return gpos;
		}

		public Vector3 getLPosition(){
			float lx = gpos.x;
			float ly = gpos.y;
			float lz = gpos.z;
			return new Vector3(lx*0.8f+0.4f,ly*0.32f,lz*0.8f+0.4f);
		}

		public Vector3 getLPosition(bool isHorizontal){
			float lx = gpos.x;
			float ly = gpos.y;
			float lz = isHorizontal?gpos.z+1 : gpos.z;
			return new Vector3(lx*0.8f+0.4f,ly*0.32f,lz*0.8f+0.4f);
		}

		public Vector2 getDimen(){
			return dimen;
		}

		public ColorSpecification getColor(){
			return color;
		}

		public Vector3 getPosition(){
			return position;
		}

		public LegoType getType(){
			return type;
		}
	}
}

