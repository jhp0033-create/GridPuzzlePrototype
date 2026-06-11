import PIL.Image as Image
import json
import os
import glob

from collections import Counter

INPUT_DIR = "Input"
OUTPUT_DIR = "Output"

def extract_main_colors(img):
    """Find 5 DISTINCT colors. High threshold ensures blue/white are picked by merging shades."""
    # Sample from a decent resolution to find the true palette
    thumb = img.copy().resize((100, 100), Image.LANCZOS)
    pixels = list(thumb.getdata())
    
    color_counts = Counter(pixels)
    sorted_potential = [item[0] for item in color_counts.most_common(2000)]
    
    clusters = []
    # High threshold (20000) to ensure distinct hues (Blue, White, Purple, etc.) get their own slots
    THRESHOLD = 20000 
    
    for color in sorted_potential:
        if len(clusters) >= 5: break
        
        is_similar = False
        for cluster in clusters:
            rep_color = cluster[0]
            dist = sum((c1 - c2) ** 2 for c1, c2 in zip(color, rep_color))
            if dist < THRESHOLD:
                cluster.append(color)
                is_similar = True
                break
        
        if not is_similar:
            clusters.append([color])
    
    # Use the most frequent original color as the representative for each cluster
    distinct_colors = []
    for cluster in clusters:
        distinct_colors.append(cluster[0])
    
    return {i+1: color for i, color in enumerate(distinct_colors)}

def get_color_type(rgb, dynamic_color_map):
    """Map any pixel to the absolute NEAREST color among the 5 in the palette."""
    best_match = 1
    min_dist = float('inf')
    
    for color_idx, target_rgb in dynamic_color_map.items():
        dist = sum((c1 - c2) ** 2 for c1, c2 in zip(rgb, target_rgb))
        if dist < min_dist:
            min_dist = dist
            best_match = color_idx
            
    return best_match

def analyze_image(image_path, grid_w=14, grid_h=16):
    img = Image.open(image_path).convert('RGB')
    
    # Step 1: Extract 5 representative colors (Guaranteed to include distinct hues)
    dynamic_color_map = extract_main_colors(img)
    print(f"Extracted Palette for {os.path.basename(image_path)}: {list(dynamic_color_map.values())}")

    # Step 2: Resize using NEAREST to prevent color bleeding/blurring!
    # This ensures grid pixels remain close to original palette colors.
    img_for_analysis = img.resize((grid_w, grid_h), Image.NEAREST)
    pixels = img_for_analysis.load()

    voxel_list = []
    grid = [[0 for _ in range(grid_w)] for _ in range(grid_h)]
    
    # PASS 1: Build logical grid
    for y in range(grid_h):
        for x in range(grid_w):
            grid[y][x] = get_color_type(pixels[x, y], dynamic_color_map)

    # PASS 2: Create 224 voxels (14x16)
    for y in range(grid_h):
        for x in range(grid_w):
            color_idx = grid[y][x]
            
            # Simple, accurate logic for a solid 14x16 grid: 
            # Only the absolute physical boundaries are initially exposed.
            exposed_mask = 0
            if y == 0: exposed_mask |= 1 # Top (y=0 in image is top)
            if x == grid_w - 1: exposed_mask |= 2 # Right
            if y == grid_h - 1: exposed_mask |= 4 # Bottom
            if x == 0: exposed_mask |= 8 # Left

            voxel_list.append({
                "uniqueID": f"voxel_{x}_{y}",
                "colorType": color_idx,
                "gridPosition": {"x": x, "y": grid_h - 1 - y},
                "exposedFaces": exposed_mask,
                "isAbsorbed": False
            })

    output = {
        "levelName": os.path.basename(image_path),
        "gridSize": {"x": grid_w, "y": grid_h},
        "palette": [{"r": int(c[0]), "g": int(c[1]), "b": int(c[2])} for c in dynamic_color_map.values()],
        "voxels": voxel_list
    }

    base_name = os.path.splitext(os.path.basename(image_path))[0]
    output_path = os.path.join(OUTPUT_DIR, f"{base_name}_LevelData.json")
    
    with open(output_path, 'w') as f:
        json.dump(output, f, indent=4)
    print(f"Success: {output_path} with 224 voxels exported.")

def main():
    if not os.path.exists(OUTPUT_DIR): os.makedirs(OUTPUT_DIR)
    files = glob.glob(os.path.join(INPUT_DIR, "*.png")) + glob.glob(os.path.join(INPUT_DIR, "*.jpg"))
    for f in files:
        analyze_image(f)

if __name__ == "__main__":
    main()
