'''
스파이크(국소 찢어짐) 탐지: 엣지 길이가 레스트 대비 몇 배로 늘어나는지 본다.
전체 변위가 큰 것(정상 모션)과 국소 찢어짐을 구분하는 정확한 지표.
'''
import bpy, sys, statistics
from collections import Counter
argv=sys.argv[sys.argv.index("--")+1:]
fbx = argv[0]
clips = argv[1].split(",") if len(argv)>1 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.fbx_import(filepath=fbx)
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
mesh=max((o for o in bpy.data.objects if o.type=='MESH'), key=lambda o: len(o.data.vertices))
scn=bpy.context.scene
groups={g.index:g.name for g in mesh.vertex_groups}
edges=[(e.vertices[0], e.vertices[1]) for e in mesh.data.edges]

def deformed():
    dg=bpy.context.evaluated_depsgraph_get(); ev=mesh.evaluated_get(dg)
    me=ev.to_mesh(); mw=ev.matrix_world
    out=[mw @ v.co for v in me.vertices]
    ev.to_mesh_clear(); return out

if arm.animation_data: arm.animation_data.action=None
bpy.context.view_layer.update()
rest=deformed()
rlen=[(rest[a]-rest[b]).length for a,b in edges]

def dom(i):
    gw=sorted(((e.weight, groups.get(e.group,"?")) for e in mesh.data.vertices[i].groups), reverse=True)
    return gw[0][1] if gw else "(none)"

for act in sorted(bpy.data.actions, key=lambda x:x.name):
    if clips and not any(c.lower() in act.name.lower() for c in clips): continue
    if arm.animation_data is None: arm.animation_data_create()
    arm.animation_data.action=act
    sl=list(getattr(act,"slots",[]))
    if sl: arm.animation_data.action_slot=sl[0]
    f0,f1=[int(round(v)) for v in act.frame_range]
    best=[1.0]*len(edges); bf=[0]*len(edges)
    step=max(1,(f1-f0)//40)
    for f in range(f0,f1+1,step):
        scn.frame_set(f)
        cur=deformed()
        for k,(a,b) in enumerate(edges):
            r=rlen[k]
            if r<1e-7: continue
            s=(cur[a]-cur[b]).length/r
            if s>best[k]: best[k]=s; bf[k]=f
    mx=max(best); med=statistics.median(best)
    bad=[k for k in range(len(edges)) if best[k]>3.0]
    print(f"\n=== {act.name}: 엣지 늘어남 median {med:.2f}x  max {mx:.1f}x   3배 초과 엣지 {len(bad)}개")
    order=sorted(range(len(edges)), key=lambda k:-best[k])[:12]
    blame=Counter()
    for k in order:
        a,b=edges[k]
        print(f"   edge v{a}-v{b} stretch={best[k]:7.1f}x @f{bf[k]:4d}  rest={[round(x,3) for x in rest[a]]}  bones: {dom(a)} / {dom(b)}")
    for k in sorted(range(len(edges)), key=lambda k:-best[k])[:200]:
        a,b=edges[k]; blame[dom(a)]+=1; blame[dom(b)]+=1
    print(f"   상위 200 엣지 지배본: {dict(blame.most_common(6))}")
