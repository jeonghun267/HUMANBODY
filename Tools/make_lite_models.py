"""떼로 나오는 캐릭터(세균·바이러스) 모형의 가벼운 판을 만든다.

Meshy 원본 glb(삼각형 약 1만 2천 개)를 텍스처 좌표를 지키며 줄인다(이차 오차 모서리 접기).
결과는 Tools/lite/{이름}.json: 정점(glTF 좌표)·UV·삼각형. Unity 쪽(JourneyRealism.LiteCharacters)이
원래 관절 메시에 맞춰 좌표를 맞추고, 가까운 원래 정점의 법선·뼈 가중치를 옮겨 '{이름}_가벼움' 프리팹을 만든다.

사용: python Tools/make_lite_models.py   (pymeshlab 필요: pip install pymeshlab)
"""
import json
from pathlib import Path

import pymeshlab

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Tools" / "lite"
# 이름: (원본 glb, 목표 삼각형 수 — UV 경계를 지키느라 이보다 조금 많이 남을 수 있다)
MODELS = {
    "Char_Probiotic": ("Assets/Models/Meshy/Char_Probiotic.glb", 3000),
    "Char_HarmfulBacteria": ("Assets/Models/Meshy/Char_HarmfulBacteria.glb", 3000),
    "Char_Virus": ("Assets/Models/Meshy/Char_Virus.glb", 3000),
    # 소장 혈관 속 적혈구(사용자 모형): 지름 10~20cm 로 수십 개가 흐르므로 가볍게
    "RBC": ("Assets/Enviroment/red_blood_cells_erythrocytes.glb", 700),
}


def lite(name: str) -> dict:
    source, target = MODELS[name]
    ms = pymeshlab.MeshSet()
    ms.load_new_mesh(str(ROOT / source))
    before = ms.current_mesh().face_number()
    textured = True
    for _ in range(2):  # 한 번에 목표까지 못 가면 한 번 더
        try:
            ms.meshing_decimation_quadric_edge_collapse_with_texture(
                targetfacenum=target, qualitythr=0.6, extratcoordw=1.0, preserveboundary=True,
                boundaryweight=1.0, optimalplacement=True, preservenormal=True, planarquadric=True)
        except pymeshlab.PyMeshLabException:
            # 텍스처 좌표가 없는 면이 섞인 모형: 모양만 줄이고, UV 는 Unity 쪽에서 가까운 원래 정점 것을 쓴다
            textured = False
            ms.meshing_decimation_quadric_edge_collapse(
                targetfacenum=target, qualitythr=0.6, preserveboundary=True, optimalplacement=True,
                preservenormal=True, planarquadric=True)
        if ms.current_mesh().face_number() <= target * 1.1:
            break
    m = ms.current_mesh()
    if not textured:
        verts = [[round(float(x), 6) for x in p] for p in m.vertex_matrix()]
        tris = [int(i) for face in m.face_matrix() for i in face]
        return {"name": name, "facesBefore": before, "facesAfter": len(tris) // 3, "verts": verts, "uvs": [], "tris": tris}
    pos, faces, wedge = m.vertex_matrix(), m.face_matrix(), m.wedge_tex_coord_matrix()
    # 꼭짓점마다 UV 가 다를 수 있다(UV 이음매) → (정점, UV) 쌍마다 정점 하나
    index, verts, uvs, tris = {}, [], [], []
    for f, face in enumerate(faces):
        for k in range(3):
            v = int(face[k]); u = wedge[f * 3 + k]
            key = (v, round(float(u[0]), 6), round(float(u[1]), 6))
            if key not in index:
                index[key] = len(verts)
                verts.append([round(float(x), 6) for x in pos[v]])
                uvs.append([key[1], key[2]])
            tris.append(index[key])
    return {"name": name, "facesBefore": before, "facesAfter": len(faces), "verts": verts, "uvs": uvs, "tris": tris}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name in MODELS:
        data = lite(name)
        (OUT / f"{name}.json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
        print(f"{name}: 삼각형 {data['facesBefore']} → {data['facesAfter']}, 정점 {len(data['verts'])}")


if __name__ == "__main__":
    main()
