"""Output-based atlas compaction. Geometry/material sources are not rewritten.

The first bake proves which charts are constant in BOTH output textures. A
second bake evaluates the original graph at the improved final UV density.
"""
import colorsys
import heapq
import math
from collections import defaultdict

import numpy as np


def _complete_hair_seams(mesh, selected, edge_faces):
    """Open handles and pinched sheets without cutting the surface into smart-UV tiles.

    Keep a maximal dual tree across the requested seams so surfaces with handles
    or pinched boundaries become solvable disks. Cuts change only UV continuity;
    source geometry and existing root-to-tip/boundary seams stay intact.
    """
    parents={face:face for face in selected}
    def find(face):
        while parents[face]!=face:
            parents[face]=parents[parents[face]];face=parents[face]
        return face
    candidates=[];extra=set()
    for edge in mesh.edges:
        faces=[f for f in edge_faces[tuple(sorted(edge.vertices))] if f in selected]
        if len(faces)!=2 or edge.use_seam:continue
        a,b=faces
        def directed(face):
            ids=list(mesh.polygons[face].vertices)
            return next((x,y) for x,y in zip(ids,ids[1:]+ids[:1]) if {x,y}==set(edge.vertices))
        if directed(a)==directed(b):
            extra.add(edge.index)
            continue
        # Prefer retaining smooth, long adjacencies; reserve sharper/shorter
        # edges for cuts. Tie breaking is independent of collection iteration.
        length=(mesh.vertices[edge.vertices[0]].co-mesh.vertices[edge.vertices[1]].co).length
        weight=length*(1.1+max(-1.,min(1.,mesh.polygons[a].normal.dot(mesh.polygons[b].normal))))
        candidates.append((-weight,edge.index,a,b))
    for _,index,a,b in sorted(candidates):
        a,b=find(a),find(b)
        if a!=b:parents[a]=b
        else:extra.add(index)
    for index in extra:mesh.edges[index].use_seam=True
    return len(extra)


def unwrap_hair(obj, flags, protected, color_keys=None):
    """Cut one root-to-tip seam per hair component instead of normal-based fragments."""
    import bpy
    mesh=obj.data;selected={p.index for p,flag in zip(mesh.polygons,flags) if flag and p.index not in protected}
    if not selected:return {'components':0,'fallback':False}
    uv=mesh.uv_layers['BakeUV'];backup=[tuple(v.uv) for v in uv.data]
    edge_faces=defaultdict(list);graph=defaultdict(dict);edge_indices={}
    for edge in mesh.edges:edge_indices[tuple(sorted(edge.vertices))]=edge.index
    for face in mesh.polygons:
        ids=list(face.vertices)
        for a,b in zip(ids,ids[1:]+ids[:1]):
            key=tuple(sorted((a,b)));edge_faces[key].append(face.index)
            if face.index in selected:
                length=(mesh.vertices[a].co-mesh.vertices[b].co).length
                graph[a][b]=length;graph[b][a]=length
    previous_seams=[e.use_seam for e in mesh.edges]
    profiles=mesh.attributes.get('SurfaceProfile')
    controls=mesh.color_attributes.get('BakedSurfaceSource') or mesh.color_attributes.get('PaletteData')
    def profile(index):
        if profiles and profiles.domain=='FACE':return int(profiles.data[index].value)
        if controls:return min(15,int(controls.data[mesh.polygons[index].loop_start].color[3]*16))
        return None
    for key,faces in edge_faces.items():
        if any(f in selected for f in faces):
            # Categorical material IDs must not bleed across narrow adjacent
            # triangles at the final texture resolution. Give each profile its
            # own chart/gutter instead of averaging or guessing its identity.
            mesh.edges[edge_indices[key]].use_seam=(len(faces)!=2
                or not all(f in selected for f in faces)
                or len({profile(f) for f in faces})>1
                or (color_keys is not None and len({color_keys[f] for f in faces})>1))
    remaining=set(graph);components=[]
    while remaining:
        seed=min(remaining);remaining.remove(seed);todo=[seed];component=set()
        while todo:
            v=todo.pop();component.add(v)
            for n in graph[v]:
                if n in remaining:remaining.remove(n);todo.append(n)
        components.append(component)
    progress=mesh.attributes.get('WGameHairProgress')
    for component in components:
        root=min(component,key=lambda v:(progress.data[v].value if progress else -mesh.vertices[v].co.z,v))
        distance={root:0.};previous={};queue=[(0.,root)]
        while queue:
            d,v=heapq.heappop(queue)
            if d!=distance[v]:continue
            for n,length in graph[v].items():
                candidate=d+length
                if candidate<distance.get(n,math.inf):
                    distance[n]=candidate;previous[n]=v;heapq.heappush(queue,(candidate,n))
        tip=max(component,key=lambda v:(distance.get(v,0.),v))
        while tip in previous:
            parent=previous[tip];mesh.edges[edge_indices[tuple(sorted((tip,parent)))]].use_seam=True;tip=parent
    topology_cuts=0;initial_invalid=0
    for v in mesh.vertices:v.select=False
    for e in mesh.edges:e.select=False
    for p in mesh.polygons:p.hide=p.index not in selected;p.select=p.index in selected
    bpy.context.scene.tool_settings.mesh_select_mode=(False,False,True)
    def solve(faces):
        for polygon in mesh.polygons:
            polygon.hide=polygon.index not in faces;polygon.select=polygon.index in faces
        bpy.ops.object.mode_set(mode='EDIT')
        import bmesh
        bm=bmesh.from_edit_mesh(mesh);layer=bm.loops.layers.uv.get('BakeUV')
        for face in bm.faces:
            for loop in face.loops:loop[layer].pin_uv=False
        bmesh.update_edit_mesh(mesh,loop_triangles=False,destructive=False)
        bpy.ops.uv.unwrap(method='MINIMUM_STRETCH',no_flip=True,iterations=30,margin=.001)
        bpy.ops.object.mode_set(mode='OBJECT')

    def inspect():
        invalid=set();kept=0;diagnostics=[];uv=mesh.uv_layers['BakeUV']
        for group in charts(mesh,{p.index for p in mesh.polygons if p.index not in selected}):
            positive=[];negative=[];degenerate=[]
            for index in group:
                face=mesh.polygons[index]
                coords=np.array([uv.data[i].uv[:] for i in face.loop_indices])
                e,f=coords[1]-coords[0],coords[2]-coords[0];area=e[0]*f[1]-e[1]*f[0]
                if not np.isfinite(coords).all() or (abs(area)<1e-12 and face.area>1e-10):degenerate.append(index)
                elif abs(area)>1e-12:(positive if area>0 else negative).append(index)
            minority=positive if len(positive)<len(negative) else negative
            bad=set(degenerate+minority)
            if bad:diagnostics.append({'faces':len(group),'degenerate':len(degenerate),'reversed':len(minority)})
            if len(bad)>max(2,len(group)*.01):invalid.update(group)
            else:invalid.update(bad);kept+=1
        return invalid,kept,diagnostics

    try:
        solve(selected)
        invalid,kept,diagnostics=inspect()
        initial_invalid=len(invalid)
        if invalid:
            # Repair only charts that actually failed. Simple hair tubes retain
            # their original sparse root-to-tip cut and successful UV solution.
            topology_cuts=_complete_hair_seams(mesh,invalid,edge_faces)
            solve(invalid)
            invalid,kept,diagnostics=inspect()
        uv=mesh.uv_layers['BakeUV']
        for face in mesh.polygons:
            for i in face.loop_indices:
                if face.index in invalid or face.index not in selected:uv.data[i].uv=backup[i]
                else:uv.data[i].uv*=.35
    finally:
        if obj.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        for p in mesh.polygons:p.hide=False;p.select=True
        for edge,value in zip(mesh.edges,previous_seams):edge.use_seam=value
    return {'components':len(components),'topologyCuts':topology_cuts,'initialInvalidFaces':initial_invalid,'fallback':bool(invalid),'fallbackFaces':len(invalid),'keptCharts':kept,'diagnostics':diagnostics}


def charts(mesh, excluded=(), face_keys=None):
    uv=mesh.uv_layers['BakeUV'];edges=defaultdict(list)
    remaining={p.index for p in mesh.polygons if p.index not in excluded}
    for index in sorted(remaining):
        loops=list(mesh.polygons[index].loop_indices)
        for a,b in zip(loops,loops[1:]+loops[:1]):
            key=tuple(sorted(((mesh.loops[a].vertex_index,tuple(round(v,7) for v in uv.data[a].uv)),
                              (mesh.loops[b].vertex_index,tuple(round(v,7) for v in uv.data[b].uv)))))
            edges[(key,face_keys.get(index) if face_keys else None)].append(index)
    adjacent=defaultdict(set)
    for group in edges.values():
        for index in group:adjacent[index].update(group)
    result=[]
    while remaining:
        seed=min(remaining);remaining.remove(seed);todo=[seed];group=[]
        while todo:
            index=todo.pop();group.append(index)
            for n in adjacent[index]:
                if n in remaining:remaining.remove(n);todo.append(n)
        result.append(sorted(group))
    return result


def encode_color(values):
    rgb=np.maximum(values[:,:3],0)
    srgb=np.where(rgb<=.0031308,rgb*12.92,1.055*np.power(rgb,1/2.4)-.055)
    return np.rint(np.clip(srgb,0,1)*255).astype(np.uint8)


def decode_color(value):
    value=np.asarray(value,dtype=np.float32)/255
    return np.where(value<=.04045,value/12.92,((value+.055)/1.055)**2.4)


def coverage(mesh, faces, size):
    """Pixel footprint including the one-texel bilinear support, not the bbox."""
    uv=mesh.uv_layers['BakeUV']
    loops=[i for index in faces for i in mesh.polygons[index].loop_indices]
    coordinates=np.asarray([uv.data[i].uv[:] for i in loops],dtype=np.float64)
    low=np.maximum(0,np.floor(coordinates.min(axis=0)*size).astype(int)-1)
    high=np.minimum(size-1,np.ceil(coordinates.max(axis=0)*size).astype(int)+1)
    mask=np.zeros((high[1]-low[1]+1,high[0]-low[0]+1),dtype=bool)
    for index in faces:
        p=np.asarray([uv.data[i].uv[:] for i in mesh.polygons[index].loop_indices],dtype=np.float64)*size-low
        lo=np.maximum(0,np.floor(p.min(axis=0)).astype(int));hi=np.minimum(np.array(mask.shape[::-1])-1,np.ceil(p.max(axis=0)).astype(int))
        if np.any(hi<lo):continue
        x,y=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
        e,f=p[1]-p[0],p[2]-p[0];det=e[0]*f[1]-e[1]*f[0]
        if abs(det)<1e-14:continue
        dx,dy=x-p[0,0],y-p[0,1]
        a=(dx*f[1]-dy*f[0])/det;b=(e[0]*dy-e[1]*dx)/det
        mask[lo[1]:hi[1]+1,lo[0]:hi[0]+1]|=(a>=-1e-6)&(b>=-1e-6)&(a+b<=1.000001)
    # Never infer a constant from an unsampled sliver.
    if not np.any(mask):return loops,coordinates,None
    expanded=mask.copy()
    expanded[1:,:]|=mask[:-1,:];expanded[:-1,:]|=mask[1:,:]
    expanded[:,1:]|=mask[:,:-1];expanded[:,:-1]|=mask[:,1:]
    y,x=np.nonzero(expanded)
    return loops,coordinates,(y+low[1])*size+x+low[0]


def _color_order(rgb):
    r,g,b=[float(x)/255 for x in rgb]
    h,s,v=colorsys.rgb_to_hsv(r,g,b)
    return (0 if s<.08 else 1,round(h*16),round(v*32),tuple(rgb))


def _overlaps(a,b):
    """Positive triangle intersection in authoring UVs; touching isn't reuse."""
    b_low=b.min(axis=1);b_high=b.max(axis=1)
    for triangle in a:
        candidates=b[np.all(b_high>triangle.min(axis=0)+1e-10,axis=1)&np.all(b_low<triangle.max(axis=0)-1e-10,axis=1)]
        for other in candidates:
            edges=np.concatenate((np.roll(triangle,-1,axis=0)-triangle,np.roll(other,-1,axis=0)-other))
            axes=np.column_stack((-edges[:,1],edges[:,0]))
            axes=axes[np.linalg.norm(axes,axis=1)>1e-12]
            axes/=np.linalg.norm(axes,axis=1)[:,None]
            x=triangle@axes.T;y=other@axes.T
            if np.all(np.minimum(x.max(axis=0),y.max(axis=0))-np.maximum(x.min(axis=0),y.min(axis=0))>1e-10):return True
    return False


def share_detail_records(records, reuse_keys):
    """Pack compatible overlapping source charts with one affine UV transform.

    Compatibility is certified from the source graph, before baking. Partial
    overlap and different triangulations work because grouping uses source UV
    coverage rather than matching vertices or low-resolution baked pixels.
    """
    candidates=defaultdict(list)
    for record in records:
        if record['constant']:continue
        obj=record['object'];mesh=obj.data
        keys=(reuse_keys or {}).get(obj.name,{})
        key=keys.get(record['faces'][0])
        if key is None or any(keys.get(i)!=key for i in record['faces']):continue
        layer=mesh.uv_layers.get('DetailUV') or mesh.uv_layers.get('FaceDetailUV')
        if layer is None:continue
        coords=np.asarray([layer.data[i].uv[:] for i in record['loops']],dtype=np.float64)
        if not np.isfinite(coords).all():continue
        triangles=coords.reshape((-1,3,2))
        cross=(triangles[:,1,0]-triangles[:,0,0])*(triangles[:,2,1]-triangles[:,0,1])-(triangles[:,1,1]-triangles[:,0,1])*(triangles[:,2,0]-triangles[:,0,0])
        if np.any(abs(cross)<1e-12):continue
        # Keep the probe's common source-UV density, even if it rotated charts.
        source_edges=np.roll(triangles,-1,axis=1)-triangles
        probe=record['coords'].reshape((-1,3,2))
        probe_edges=np.roll(probe,-1,axis=1)-probe
        lengths=np.linalg.norm(source_edges,axis=2)
        density=float(np.median(np.linalg.norm(probe_edges,axis=2)[lengths>1e-10]/lengths[lengths>1e-10]))
        candidates[key].append((record,coords,triangles,density))
    replacements={};removed=set();groups=0;reused=0;shared_faces=0
    for items in candidates.values():
        adjacent=defaultdict(set)
        for i,a in enumerate(items):
            for j in range(i):
                b=items[j]
                if _overlaps(a[2],b[2]):adjacent[i].add(j);adjacent[j].add(i)
        remaining=set(range(len(items)))
        while remaining:
            seed=min(remaining);remaining.remove(seed);component=[];todo=[seed]
            while todo:
                i=todo.pop();component.append(items[i])
                for j in sorted(adjacent[i]):
                    if j in remaining:remaining.remove(j);todo.append(j)
            if len(component)<2:continue
            component.sort(key=lambda item:item[0]['index'])
            first=component[0][0];merged=dict(first)
            coords=np.concatenate([item[1] for item in component]);low=coords.min(axis=0);span=coords.max(axis=0)-low
            merged.update(coords=coords,low=low,span=span,detail=True,
                          natural=span*max(item[3] for item in component),
                          area=sum(item[0]['area'] for item in component),
                          parts=[(item[0]['object'],item[0]['loops'],item[1]) for item in component])
            replacements[first['index']]=merged
            removed.update(item[0]['index'] for item in component[1:])
            groups+=1;reused+=len(component)-1;shared_faces+=sum(len(item[0]['faces']) for item in component)
    result=[replacements.get(r['index'],r) for r in records if r['index'] not in removed]
    for index,record in enumerate(result):record['index']=index
    return result,{'sharedDetailGroups':groups,'reusedDetailCharts':reused,'sharedDetailFaces':shared_faces}


def compact(meshes, duplicates, color_pixels, control_pixels, size, protected=None, reuse_keys=None, flat_color_faces=None, color_is_srgb=False):
    rgb=np.rint(np.clip(color_pixels[:,:3],0,1)*255).astype(np.uint8) if color_is_srgb else encode_color(color_pixels)
    controls=np.rint(np.clip(control_pixels,0,1)*255).astype(np.uint8)
    records=[];constant_groups=defaultdict(list);probe_charts=0;split_constant_faces=0
    for obj in meshes:
        mesh=obj.data
        protected_faces=(protected or {}).get(obj.name,set())
        proven_flat=(flat_color_faces or {}).get(obj.name,set())
        hair=mesh.attributes.get('WGameBakeHair')
        def analyze(faces):
            loops,coords,indices=coverage(mesh,faces,size)
            colors=rgb[indices] if indices is not None else None
            data=controls[indices] if indices is not None else None
            color_constant=colors is not None and np.all(colors==colors[0])
            authored_detail=any(i in protected_faces for i in faces)
            is_constant=bool(color_constant and np.all(data==data[0]) and not authored_detail)
            low=coords.min(axis=0);span=np.maximum(coords.max(axis=0)-low,1e-9)
            record={'object':obj,'faces':faces,'loops':loops,'coords':coords,'low':low,'span':span,
                    'detail':authored_detail or (not color_constant and not all(i in proven_flat for i in faces)),
                    'area':sum(mesh.polygons[i].area for i in faces),
                    'rgb':tuple(colors.mean(axis=0).astype(int)) if colors is not None else (128,128,128),
                    'index':len(records),'constant':is_constant}
            if is_constant:
                key=tuple(int(v) for v in colors[0])+tuple(int(v) for v in data[0])
                record['key']=key
            return record
        def add(record):
            record['index']=len(records);records.append(record)
            if record['constant']:constant_groups[record['key']].append(record)
        extracted=set()
        original_charts=charts(mesh,duplicates[obj.name]);probe_charts+=len(original_charts)
        for faces in original_charts:
            record=analyze(faces)
            if record['constant']:
                add(record);extracted.update(faces)
                continue
            # A multicolored chart can contain individually constant faces.
            # Classify them before packing the remaining connected regions.
            # Include the full texel footprint, and never collapse authored
            # detail based on a probe that may miss subpixel painting.
            for face in faces:
                # Preserve coherent geometric AO charts: a flat probe patch
                # inside hair can change when rebaked at the new density.
                if face in protected_faces or (hair and hair.data[face].value>.5):continue
                part=analyze([face])
                if part['constant']:
                    add(part);extracted.add(face);split_constant_faces+=1
        for faces in charts(mesh,set(duplicates[obj.name])|extracted,(reuse_keys or {}).get(obj.name)):
            add(analyze(faces))
    records,reuse_stats=share_detail_records(records,reuse_keys)
    pad=max(2,size//128)
    core=max(4,size//128)
    pitch=core+2*pad
    keys=sorted(constant_groups,key=lambda k:(_color_order(k[:3]),k[6],k[3:6]))
    columns=max(1,size//pitch)
    palette_height=math.ceil(len(keys)/columns)*pitch
    details=[r for r in records if not r['constant'] and r['detail']]
    surfaces=[r for r in records if not r['constant'] and not r['detail']]
    constant_cells=[]
    for i,key in enumerate(keys):
        x=(i%columns)*pitch;y=(i//columns)*pitch
        constant_cells.append((x,y,pitch,key))
        for record in constant_groups[key]:
            target=(record['coords']-record['low'])/record['span']*(core-1)+np.array((x+pad+.5,y+pad+.5))
            uv=record['object'].data.uv_layers['BakeUV']
            for loop,point in zip(record['loops'],target):uv.data[loop].uv=point/size
    import runpy
    from pathlib import Path
    packer=runpy.run_path(str(Path(__file__).with_name('baked_atlas_packing.py')))
    targets,packing_stats=packer['joint_pack'](records,constant_cells,size,pad)
    for record in records:
        if record['constant']:continue
        target=targets[record['index']];offset=0
        for obj,loops,coords in packer['parts'](record):
            uv=obj.data.uv_layers['BakeUV']
            for loop,point in zip(loops,target[offset:offset+len(loops)]):uv.data[loop].uv=point
            offset+=len(loops)
    for obj in meshes:
        uv=obj.data.uv_layers['BakeUV']
        for copy,original in duplicates[obj.name].items():
            for a,b in zip(obj.data.polygons[copy].loop_indices,obj.data.polygons[original].loop_indices):uv.data[a].uv=uv.data[b].uv
    return {'cells':constant_cells,'stats':{'probeCharts':probe_charts,'constantCharts':sum(r['constant'] for r in records),
            'splitConstantFaces':split_constant_faces,**reuse_stats,
            'sharedConstantTiles':len(keys),'detailCharts':len(details),'surfaceCharts':len(surfaces),
            'paletteRows':math.ceil(len(keys)/columns),'paletteHeight':palette_height,'surfaceBandHeight':0,
            'packingMethod':'jointConcave',**packing_stats}}


def fill_constants(layout,color,control,size,color_is_srgb=False):
    """Fill proven constant cells and gutters exactly, including unsampled slivers."""
    color=color.reshape((size,size,4));control=control.reshape((size,size,4))
    for x,y,pitch,key in layout['cells']:
        color[y:y+pitch,x:x+pitch,:3]=np.asarray(key[:3],dtype=np.float32)/255 if color_is_srgb else decode_color(key[:3])
        color[y:y+pitch,x:x+pitch,3]=1
        control[y:y+pitch,x:x+pitch,:]=np.array(key[3:],dtype=np.float32)/255
