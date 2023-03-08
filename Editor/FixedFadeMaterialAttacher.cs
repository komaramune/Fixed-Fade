using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class FixedFadeMaterialAttacher : EditorWindow {
	[MenuItem ("Fixed Fade/Material Attacher")]
	static void Do () {
		GetWindow<FixedFadeMaterialAttacher> ();
	}

	[SerializeField]
	public Material defaultZwriteMaterial;
	static Material zwriteMaterial;
	static Shader fadeShader;
	static GameObject targetObject;
	static int queueNumber;
	static bool isOperateBlendShape = false; //ブレンドシェイプを処理するか
	static AnimationClip[] animationClips = new AnimationClip[1]; //アニメーションクリップ配列
	static bool folding = false; //アニメーションクリップ欄折りたたみ
	static int animationArraySize = 1; //アニメーションクリップ欄サイズ

	//ウィンドウ起動時
	void Awake () {
		zwriteMaterial = defaultZwriteMaterial;
		if (Selection.activeObject != null) targetObject = Selection.activeObject as GameObject;
		if (zwriteMaterial != null) queueNumber = zwriteMaterial.renderQueue + 1;
	}

	void OnGUI () {
		GUIStyle ButtonStyle = new GUIStyle (GUI.skin.button);
		ButtonStyle.fontSize = 30;
		fadeShader = EditorGUILayout.ObjectField ("半透明にするシェーダー", fadeShader, typeof (Shader), false) as Shader;
		EditorGUILayout.LabelField ("");
		zwriteMaterial = EditorGUILayout.ObjectField ("ZWriteマテリアル", zwriteMaterial, typeof (Material), false) as Material;
		EditorGUILayout.LabelField ("");
		targetObject = EditorGUILayout.ObjectField ("オブジェクト", targetObject, typeof (GameObject), true) as GameObject;
		EditorGUILayout.LabelField ("");
		queueNumber = EditorGUILayout.IntField ("レンダーキュー", queueNumber);
		if (zwriteMaterial != null) zwriteMaterial.renderQueue = queueNumber - 1;
		EditorGUILayout.LabelField ("");
		if (isOperateBlendShape = EditorGUILayout.Toggle ("ブレンドシェイプ処理", isOperateBlendShape)) {
			EditorGUILayout.LabelField ("");
			animationArraySize = EditorGUILayout.IntField ("Size", animationArraySize);
			if (animationClips.Length != animationArraySize) Array.Resize (ref animationClips, animationArraySize);
			for (int i = 0; i < animationArraySize; i++) {
				animationClips[i] = EditorGUILayout.ObjectField ("アニメーションクリップ " + (i + 1), animationClips[i], typeof (AnimationClip), false) as AnimationClip;
			}
		}
		GUILayout.FlexibleSpace ();
		if (GUILayout.Button ("処理実行", ButtonStyle)) {
			if (zwriteMaterial != null && targetObject != null) {
				OperateObject ();
				Debug.Log ("処理が完了しました");
			} else {
				Debug.LogError ("マテリアルとオブジェクトを設定してください");
			}
		}

	}

	private static void OperateObject () {
		var instanceObject = GameObject.Instantiate (targetObject);

		//フォルダ生成
		var guid = AssetDatabase.CreateFolder ("Assets", targetObject.name + " SubMeshes");
		var folderName = AssetDatabase.GUIDToAssetPath (guid);
		int assetIndex = 0;

		//コンポーネント取得
		var skinnedMeshRenderers = instanceObject.GetComponentsInChildren (typeof (SkinnedMeshRenderer));
		var meshRenderers = instanceObject.GetComponentsInChildren (typeof (MeshRenderer));

		//変換したオブジェクトの辞書
		Dictionary<string, List<string>> ConvertedDict = new Dictionary<string, List<string>> ();

		//スキンメッシュ処理
		if (skinnedMeshRenderers != null) {
			foreach (SkinnedMeshRenderer orignalRenderer in skinnedMeshRenderers) {
				var orignalObject = orignalRenderer.gameObject;
				int subMeshCount = orignalRenderer.sharedMesh.subMeshCount;
				if (subMeshCount > 1) {
					//メッシュ生成
					List<Mesh> meshlist = SplitSubMesh (orignalRenderer.sharedMesh);
					//メッシュ出力
					foreach (Mesh mesh in meshlist) {
						AssetDatabase.CreateAsset (mesh, folderName + "/SubMesh(" + assetIndex + ").asset");
						assetIndex++;
					}
					//オブジェクト複製
					int duplicate = 0;
					for (int i = 1; i < subMeshCount; i++) {
						var cloneObject = UnityEngine.Object.Instantiate (orignalObject) as GameObject;

						while (orignalObject.transform.parent.Find (orignalObject.transform.name + " (" + (i + duplicate) + ")") != null) {
							duplicate++;
						}
						var name = orignalObject.transform.name + " (" + (i + duplicate) + ")";
						cloneObject.transform.name = name;
						if (ConvertedDict.ContainsKey (orignalObject.transform.name)) {
							ConvertedDict[orignalObject.transform.name].Add (name);
						} else {
							ConvertedDict[orignalObject.transform.name] = new List<string> () { name };
						}

						cloneObject.transform.parent = orignalObject.transform.parent;
						cloneObject.transform.SetSiblingIndex (orignalObject.transform.GetSiblingIndex () + 1 + i);
						var cloneSkinnedMeshRenderer = cloneObject.GetComponent (typeof (SkinnedMeshRenderer)) as SkinnedMeshRenderer;
						cloneSkinnedMeshRenderer.sharedMaterials = new Material[] { zwriteMaterial, orignalRenderer.sharedMaterials[i] };
						cloneSkinnedMeshRenderer.sharedMesh = meshlist[i];
					}
					//元オブジェクト処理
					orignalRenderer.sharedMesh = meshlist[0];
				}
				//シェーダー&レンダーキュー変更
				foreach (Material material in orignalRenderer.sharedMaterials) {
					if (fadeShader != null) material.shader = fadeShader;
					material.renderQueue = queueNumber;
				}
				orignalRenderer.sharedMaterials = new Material[] { zwriteMaterial, orignalRenderer.sharedMaterial };
			}
		}

		//ノーマルメッシュ処理
		if (meshRenderers != null) {
			foreach (MeshRenderer orignalRenderer in meshRenderers) {
				var orignalObject = orignalRenderer.gameObject;
				var orignalFilter = orignalObject.GetComponent<MeshFilter> ();
				int subMeshCount = orignalFilter.sharedMesh.subMeshCount;
				if (subMeshCount > 1) {
					//メッシュ生成
					List<Mesh> meshlist = SplitSubMesh (orignalFilter.sharedMesh);
					//メッシュ出力
					foreach (Mesh mesh in meshlist) {
						AssetDatabase.CreateAsset (mesh, folderName + "/SubMesh(" + assetIndex + ").asset");
						assetIndex++;
					}
					//オブジェクト複製
					int duplicate = 0;
					for (int i = 1; i < subMeshCount; i++) {
						var cloneObject = UnityEngine.Object.Instantiate (orignalObject) as GameObject;
						//オブジェクトがルートだった場合
						if(orignalObject.transform.parent == null){
							GameObject parent = new GameObject();
							parent.transform.name = orignalObject.transform.name;
							orignalObject.transform.parent = parent.transform;
						}

						while (orignalObject.transform.parent.Find (orignalObject.transform.name + " (" + (i + duplicate) + ")") != null) {
							duplicate++;
						}
						var name = orignalObject.transform.name + " (" + (i + duplicate) + ")";
						cloneObject.transform.name = name;
						if (ConvertedDict.ContainsKey (orignalObject.transform.name)) {
							ConvertedDict[orignalObject.transform.name].Add (name);
						} else {
							ConvertedDict[orignalObject.transform.name] = new List<string> () { name };
						}

						cloneObject.transform.parent = orignalObject.transform.parent;
						cloneObject.transform.SetSiblingIndex (orignalObject.transform.GetSiblingIndex () + 1 + i);
						var cloneMeshFilter = cloneObject.GetComponent (typeof (MeshFilter)) as MeshFilter;
						var cloneMeshRenderer = cloneObject.GetComponent (typeof (MeshRenderer)) as MeshRenderer;
						cloneMeshRenderer.sharedMaterials = new Material[] { zwriteMaterial, orignalRenderer.sharedMaterials[i] };
						cloneMeshFilter.sharedMesh = meshlist[i];
					}
					//元オブジェクト処理
					orignalFilter.sharedMesh = meshlist[0];
				}
				//シェーダー&レンダーキュー変更
				foreach (Material material in orignalRenderer.sharedMaterials) {
					if (fadeShader != null) material.shader = fadeShader;
					material.renderQueue = queueNumber;
				}
				orignalRenderer.sharedMaterials = new Material[] { zwriteMaterial, orignalRenderer.sharedMaterial };
			}
		}

		//後処理
		targetObject.SetActive (false);
		if (isOperateBlendShape) ChangeAnimation (animationClips, ConvertedDict);
		AssetDatabase.Refresh ();
	}

	//メッシュ操作はこちらのオノッチさんの解説が大変参考になりました https://onoty3d.hatenablog.com/entry/2015/12/09/000000
	private static List<Mesh> SplitSubMesh (Mesh mesh) {

		//サブメッシュの出力
		var title = "Export";
		var info = "Exporting subMesh {0} {1}/{2} ...";
		var meshlist = new List<Mesh> ();
		try {
			for (int i = 0; i < mesh.subMeshCount; i++) {
				EditorUtility.DisplayProgressBar (title, string.Format (info, mesh.name, i + 1, mesh.subMeshCount), (float) (i + 1) / (float) mesh.subMeshCount);
				meshlist.Add (CreateMesh (mesh, i));
			}
		} finally {
			EditorUtility.ClearProgressBar ();
		}
		return meshlist;
	}

	private static Mesh CreateMesh (Mesh orignal, int index) {
		//サブメッシュの三角形リスト取得
		var triangles = orignal.GetTriangles (index);

		//サブメッシュの三角形リスト(同値省く)取得
		var trianglesUnique = triangles.Distinct ().ToList ();

		//サブメッシュの頂点位置とテクスチャ座標の切り出し
		var vertices = (orignal.vertices.Length != 0) ? trianglesUnique.Select (x => orignal.vertices[x]).ToArray () : new Vector3[0];
		var uv = (orignal.uv.Length != 0) ? trianglesUnique.Select (x => orignal.uv[x]).ToArray () : new Vector2[0];
		var normals = (orignal.normals.Length != 0) ? trianglesUnique.Select (x => orignal.normals[x]).ToArray () : new Vector3[0];
		var tangents = (orignal.tangents.Length != 0) ? trianglesUnique.Select (x => orignal.tangents[x]).ToArray () : new Vector4[0];
		var boneWeights = (orignal.boneWeights.Length != 0) ? trianglesUnique.Select (x => orignal.boneWeights[x]).ToArray () : new BoneWeight[0];
		var bindposes = orignal.bindposes;

		//三角形リストの値をサブメッシュに合わせてシフト
		var triangleNewIndexes = trianglesUnique.Select ((x, i) => new { OldIndex = x, NewIndex = i }).ToDictionary (x => x.OldIndex, x => x.NewIndex);
		var triangleOldIndexes = triangleNewIndexes.ToDictionary (x => x.Value, x => x.Key);
		var triangleConv = triangles.Select (x => triangleNewIndexes[x]).ToArray ();

		//メッシュ作成
		var mesh = new Mesh ();
		mesh.vertices = vertices;
		mesh.uv = uv;
		mesh.normals = normals;
		mesh.tangents = tangents;
		mesh.triangles = triangleConv;
		mesh.boneWeights = boneWeights;
		mesh.bindposes = bindposes;

		if (isOperateBlendShape) {
			//ブレンドシェイプコピー
			for (int i = 0; i < orignal.blendShapeCount; i++) {
				int flame = orignal.GetBlendShapeFrameCount (i);
				string name = orignal.GetBlendShapeName (i);
				Vector3[][] newDeltaVertices = new Vector3[flame][];
				Vector3[][] newDeltaNormals = new Vector3[flame][];
				Vector3[][] newDeltaTangents = new Vector3[flame][];

				for (int j = 0; j < flame; j++) {
					Vector3[] deltaVertices = new Vector3[orignal.vertexCount];
					Vector3[] deltaNormals = new Vector3[orignal.vertexCount];
					Vector3[] deltaTangents = new Vector3[orignal.vertexCount];
					orignal.GetBlendShapeFrameVertices (i, j, deltaVertices, deltaNormals, deltaTangents);
					newDeltaVertices[j] = new Vector3[vertices.Length];
					newDeltaNormals[j] = new Vector3[vertices.Length];
					newDeltaTangents[j] = new Vector3[vertices.Length];
					for (int k = 0; k < vertices.Length; k++) {
						newDeltaVertices[j][k] = deltaVertices[triangleOldIndexes[k]];
						newDeltaNormals[j][k] = deltaNormals[triangleOldIndexes[k]];
						newDeltaTangents[j][k] = deltaTangents[triangleOldIndexes[k]];
					}
					float w = orignal.GetBlendShapeFrameWeight (i, j);
					mesh.AddBlendShapeFrame (name, w, newDeltaVertices[j], newDeltaNormals[j], newDeltaTangents[j]);
				}

			}
		}

		//バウンディングボリュームと法線の再計算
		mesh.RecalculateBounds ();
		mesh.RecalculateNormals ();

		//メッシュを返す
		return mesh;
	}

	//アニメーションクリップ修正
	private static void ChangeAnimation (AnimationClip[] animationClips, Dictionary<string, List<string>> ConvertedDict) {
		for (int i = 0; i < animationClips.Length; i++) {
			if (animationClips[i] != null) {
				var bindings = AnimationUtility.GetCurveBindings (animationClips[i]).ToArray ();
				for (int j = 0; j < bindings.Length; j++) {
					var path = bindings[j].path;
					if (bindings[j].type == typeof (SkinnedMeshRenderer) && ConvertedDict.ContainsKey (path)) {
						for (int k = 0; k < ConvertedDict[path].Count; k++) {
							var curve = AnimationUtility.GetEditorCurve (animationClips[i], bindings[j]);
							bindings[j].path = ConvertedDict[path][k];
							AnimationUtility.SetEditorCurve (animationClips[i], bindings[j], curve);
						}
					}
				}
				AssetDatabase.SaveAssets ();
			}
		}
	}
}