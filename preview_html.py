import sys
from pathlib import Path
import base64
import logging
import re
from PIL import Image
import json
import time
import cv2
import numpy as np

html_sortable_table_class = "sortable_table"

# Stop after including this many items in the html
# for debbugging
files_max = None
img_resolution_output = [144, 240, 360]
allowedExtensions = {".pmx", ".pmd"}
exclude_folders_in_size = {"prev"}

keys = [
    "Preview",
    "Name",
    "Directory",
    "Location",
    "File",
    "MB",
    "Google_Tr_Dirname",
    "Google_Tr_Name",
    "Official_Dirname",
    "Official_Name",
]

html_script = """
<script>
function getCellValue(el, idx){
    try {
        return el.cells[idx].innerText.trim();
    } catch {
        return el.cells[idx].textContent.trim();
    }
    return "";
}

function compare(v1, v2) {
    // sort based on a numeric or localeCompare, based on type...
    return (v1 !== '' && v2 !== '' && !isNaN(v1) && !isNaN(v2)) 
        ? v1 - v2 
        : v1.toString().localeCompare(v2);
}

// function compare(v1, v2) {
//     return v1.localeCompare(v2);
// }

function sortTable(th, col) {
    const table = th.closest('table');
    th.asc = !th.asc;
    var reverse = -((+th.asc) || -1);
    var tb = table.tBodies[0], // use `<tbody>` to ignore `<thead>` and `<tfoot>` rows
        tr = Array.prototype.slice.call(tb.rows, 0), // put rows into array
        i;
    tr = tr.sort(function (a, b) { // sort rows
        return reverse // `-1 *` if want opposite order
            * (compare(getCellValue(a, col), getCellValue(b, col))
               );
    });
    for(i = 0; i < tr.length; ++i) tb.appendChild(tr[i]); // append each row in order
}

function hide_element_class(state, class_name) {
    state.hidden_toggle = !state.hidden_toggle;
    var d = document.getElementsByClassName(class_name);
    if (state.hidden_toggle){
        for (el of d) {
            el.style.display = 'none';

            state.style.color = 'white';
            state.style.backgroundColor = '#333';
        }
    } else {
        for (el of d) {
            el.style.display = '';
            
            state.style.color = 'black';
            state.style.backgroundColor = 'white';
        }
    }
}

function font_size(state){
    console.log("fontSize = " + state.value + "px");
    document.body.style.fontSize = state.value + "px";
}
function image_max_width(state){
    console.log("fontSize = " + state.value + "px");
    var d = document.getElementsByClassName("preview_image");
    for (el of d) {
        el.style.maxWidth = state.value + "px";
    }
}
"""

html_script += (
    """
function table_stack_columns(state){
    state.toggle = !state.toggle;
    var keys = """
    + f"{keys}"
    + """;
    for (key of keys){
        if (key === "Preview") {
            continue;
        }
        var els = document.getElementsByClassName("col_" + key);
        if (state.toggle) {
            for (el of els){
                el.style.display = "table-row";
                if (el.nodeName === "TH"){
                    el.style.border = "none";
                    el.style.fontSize = 12 + "px";
                }
            }

            state.style.color = 'white';
            state.style.backgroundColor = '#333';
        } else {
            for (el of els){
                el.style.display = "";
                if (el.nodeName === "TH"){
                    el.style.border = "1px solid #333";
                    el.style.fontSize = 24 + "px";
                }
            }

            state.style.color = 'black';
            state.style.backgroundColor = 'white';
        }
    }
}
</script>
"""
)


def main():
    start_time = time.time()

    if len(sys.argv) != 2:
        print('Usage: python preview_html.py "path_to_folder_tree_with_previews"')
        return

    dirpath = sys.argv[1]
    print(dirpath)
    if not Path(dirpath).is_dir():
        print("Path does not exist")
        return

    modelFiles = Path(dirpath).glob("**/*")
    modelFiles = sorted(
        [f for f in modelFiles if f.suffix in allowedExtensions and f.is_file()]
    )

    folder_size_cache = {}
    cache_dir = "cache/"
    cache_translations = {}

    filepath = cache_dir + "cache_translations.json"
    try:
        with open(filepath, "rb") as f:
            cache_translations = json.load(f)
    except FileNotFoundError:
        logging.warning(f"Cache file {filepath} not found!!")

    style = """
        <style>
            body {
                background-color:black;
                color: white;
            }
            .image_cell {
                display:inline-block;
                border: 3px solid #999;
                padding: 0px;
                margin: 3px;
            }
            table {
                font-family: arial, sans-serif;
                border-collapse: collapse;
                font-size: inherit;
            }
            table tr:nth-child(n) {
                background-color: #222;
            }
            table tr:nth-child(2n) {
                background-color: #333;
            }
            table td, table th {
                border: 1px solid #333;
                text-align: left;
                padding: 8px;
                padding-top: 0px;
            }
            th {
                box-shadow: "none";
                border: "none";
            }
            thead, thead tr {
                font-size: 24px;
                position: sticky;
                top: 22px; /* required for the stickiness */
                /* 1 pixel black shadow to left, top, right and bottom */
                text-shadow: -2px 0 black, 0 2px black, 2px 0 black, 0 -2px black;
                /* background-color: #222; */
                /* background: #222; */
                background: rgba(2, 2, 2, 0.0) !important;
                background-color: rgba(2, 2, 2, 0.0) !important;
                /* box-shadow: 0 2px 2px -1px rgba(0, 0, 0, 0.4); */
                box-shadow: "none";
                border: "none";
            }
            .top_header {
                text-shadow: -1px 0 black, 0 1px black, 1px 0 black, 0 -1px black;
                position: sticky;
                top: 0; /* required for the stickiness */
                font-weight: bold;
                font-size: 16px;
                overflow-x: visible;
                white-space: nowrap;
            }
            .button_hide_element_class {
                font-size: 12px;
            }
            a {
                color: hotpink;
            }
            a:visited {
                color: cyan;
            }
            .catalogue_location {
                font-size: 9px;
            }
            .preview_image {
                max-width: 380px;
            }
        """
    # table td {
    #     font-size:50px;
    # }

    style += "</style>\n"
    
    f_table = {}
    f_catalogue = {}
    for ir in img_resolution_output:
        f_table[ir] = open(
                Path(dirpath) / f"previews-{Path(dirpath).name}-{ir}p.html",
                "w",
            )
        f_catalogue[ir] = open(
            Path(dirpath)
            / f"previews-{Path(dirpath).name}-catalogue_{ir}p.html",
            "w",
        )

    with open(
        Path(dirpath) / f"previews-{Path(dirpath).name}-text.html", "w"
    ) as f_text:

        def make_html_pag_start(include_preview=True):
            html_page_table = ""
            html_page_table += "<html>"
            html_page_table += "<head>"
            html_page_table += style
            html_page_table += html_script
            html_page_table += "</head>"
            html_page_table += "<body>\n"

            html_page_table += f'<div class="top_header">\n'

            html_page_table += f"\t<span>Font size:</span> "
            html_page_table += f'\t<input onchange="font_size(this)" type="number" id="font_size" name="font_size" value="16">\n'

            html_page_table += f"\t<span>Preview max_width:</span> "
            html_page_table += f'\t<input onchange="image_max_width(this)" type="number" id="image_max_width" name="image_max_width" value="380">\n'

            html_page_table += f'\t<button onclick="table_stack_columns(this)">Toggle column stacking (unstack to sort)</button>\n'

            html_page_table += f"\t<span>Toggle table columns:</span>\n"
            s = 0
            if not include_preview:
                s = 1
            for idx, key in enumerate(keys[s:]):
                html_page_table += f'\t<button class="button_hide_element_class" onclick="hide_element_class(this, \'{f"col_{key}"}\')">{key}</button>\n'
            html_page_table += f"</div>\n"

            html_page_table += '<table class="searchable  sortable sortable_table">'
            html_page_table += "<thead>\n"
            html_page_table += "\t<tr>\n"
            for idx, key in enumerate(keys[s:]):
                html_page_table += f'\t<th class="col_{key}" onclick="sortTable(this, {idx})">{key}</th>\n'
            html_page_table += "</tr>"
            html_page_table += "</thead>\n"
            return html_page_table

        html_page_table = make_html_pag_start()
        html_page_text = make_html_pag_start(include_preview=False)

        f_text.write(html_page_text)
        for ir in img_resolution_output:
            f_table[ir].write(html_page_table)

        html_page_catalogue = ""
        html_page_catalogue += "<html>"
        html_page_catalogue += "<head>"
        html_page_catalogue += style
        html_page_catalogue += html_script
        html_page_catalogue += "</head>"
        html_page_catalogue += "<body>"
        html_page_catalogue += '<div class="image_list">'

        for ir in img_resolution_output:
            f_catalogue[ir].write(html_page_catalogue)

        for modelFile in modelFiles[:files_max]:
            name = ""
            metaPath = str(modelFile) + ".meta.txt"

            if Path(metaPath).exists():
                metadata = {}
                with open(metaPath, mode="r", encoding="utf-8") as f:
                    metadata = {
                        line.split("=")[0].strip(): line.split("=")[1].strip()
                        for line in f
                        if len(line) > 2
                    }
                if "NAME_EN" in metadata and len(metadata["NAME_EN"]) > 1:
                    name = metadata["NAME_EN"]
                elif "NAME_JP" in metadata and len(metadata["NAME_JP"]) > 1:
                    name = metadata["NAME_JP"]

            def prep_imgs(previewPaths, out_height=None):
                imgs = []
                for previewPath in previewPaths:
                    with Image.open(previewPath) as im:
                        imgs.append(np.array(im.convert("RGB"))[:, :, ::-1].copy())
                imgs_cropped = []
                rects = []
                heights = []
                for img in imgs:
                    ## (1) Convert to gray, and threshold
                    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
                    th, threshed = cv2.threshold(gray, 253, 255, cv2.THRESH_BINARY_INV)

                    ## (2) Morph-op to remove noise
                    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
                    morphed = cv2.morphologyEx(threshed, cv2.MORPH_CLOSE, kernel)

                    ## (3) Find the max-area contour
                    x, y, w, h = (0, 0, img.shape[1], img.shape[0])
                    try:
                        cnts = cv2.findContours(
                            morphed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE
                        )[-2]
                        cnt = sorted(cnts, key=cv2.contourArea)[-1]

                        ## (4) Crop and save it
                        x, y, w, h = cv2.boundingRect(cnt)
                    except:
                        pass
                    rects.append((x, y, w, h))
                    heights.append(img.shape[0])

                heights_cropped = [rect[-1] for rect in rects]
                max_height = max(heights_cropped)
                for img, rect in zip(imgs, rects):

                    y = rect[1]
                    dst = img[
                        rect[1] : rect[1] + max_height, rect[0] : rect[0] + rect[2]
                    ]
                    if dst.shape[0] < max_height:
                        dst = cv2.copyMakeBorder(
                            dst,
                            0,
                            max_height - dst.shape[0],
                            0,
                            0,
                            cv2.BORDER_CONSTANT,
                            value=(0, 0, 0),
                        )
                    imgs_cropped.append(dst)

                img_total = cv2.hconcat(imgs_cropped)
                (h, w) = img_total.shape[:2]
                if out_height and h > out_height:
                    # downsize to target out_height
                    r = out_height / float(h)
                    dim = (int(w * r), out_height)
                    img_total = cv2.resize(
                        img_total, dim, interpolation=cv2.INTER_LANCZOS4
                    )

                retval, buffer = cv2.imencode(".jpg", img_total)
                jpg_as_text = base64.b64encode(buffer)
                return jpg_as_text.decode("ascii")

            prevDir = Path(modelFile).parent / "prev"
            previewPaths = [
                prevDir / f"{Path(modelFile).name}_0, 0.png",
                prevDir / f"{Path(modelFile).name}_0, 45.png",
                prevDir / f"{Path(modelFile).name}_-50, 140.png",
            ]
            imageData = {}
            for ir in img_resolution_output:
                try:
                    imageData[ir] = prep_imgs(previewPaths, ir)
                except FileNotFoundError as e:
                    ps = [re.sub("\\\\", "/", str(p)) for p in previewPaths]
                    logging.warning(f"FileNotFoundError images: {ps}")
                except Exception as e:
                    ps = [re.sub("\\\\", "/", str(p)) for p in previewPaths]
                    logging.exception(f"Error on images: {ps}")

            dirlink = Path(modelFile).parent.relative_to(dirpath)

            PreviewHtml = {}
            for ir in img_resolution_output:
                PreviewHtml[ir] = f"<a  href=\"{dirlink}\"><img class='preview_image' src='data:image/png;base64, {imageData.get(ir, '')}'/></a>"

            def get_folder_size(folder_path, folder_size_cache={}):
                if folder_path.name in exclude_folders_in_size:
                    return 0
                if str(folder_path) in folder_size_cache:
                    return folder_size_cache[str(folder_path)]
                folder_size = sum(
                    f.stat().st_size
                    for f in Path(folder_path).glob("*")
                    if f.is_file()
                )
                folder_size += sum(
                    get_folder_size(f, folder_size_cache)
                    for f in Path(folder_path).glob("*")
                    if f.is_dir()
                )
                folder_size_cache[str(folder_path)] = folder_size
                return folder_size

            folder_size = get_folder_size(Path(modelFile).parent, folder_size_cache)

            byteScaleMB = 1024 * 1024
            folder_size = int(folder_size / byteScaleMB)

            row = {
                "Preview": PreviewHtml,
                "Name": name,
                "Directory": Path(modelFile).parent.name,
                "Location": Path(modelFile).parent.parent.relative_to(dirpath),
                "File": Path(modelFile).name,
                "MB": folder_size,
                "Official_Name": ", ".join(
                    [
                        t
                        for t in cache_translations[Path(modelFile).name]["trs"][
                            "official"
                        ]
                        if t
                    ]
                    if Path(modelFile).name in cache_translations
                    and ("official" in cache_translations[Path(modelFile).name]["trs"])
                    and (cache_translations[Path(modelFile).name]["trs"]["official"])
                    else []
                ),
                "Official_Dirname": ", ".join(
                    [
                        t
                        for t in cache_translations[Path(modelFile).parent.name]["trs"][
                            "official"
                        ]
                        if t
                    ]
                    if Path(modelFile).parent.name in cache_translations
                    and (
                        "official"
                        in cache_translations[Path(modelFile).parent.name]["trs"]
                    )
                    and (
                        cache_translations[Path(modelFile).parent.name]["trs"][
                            "official"
                        ]
                    )
                    else []
                ),
                "Google_Tr_Name": ", ".join(
                    [
                        t
                        for t in cache_translations[Path(modelFile).name]["trs"][
                            "google"
                        ]
                        if t
                    ]
                    if Path(modelFile).name in cache_translations
                    and ("google" in cache_translations[Path(modelFile).name]["trs"])
                    and (cache_translations[Path(modelFile).name]["trs"]["google"])
                    else []
                ),
                "Google_Tr_Dirname": ", ".join(
                    [
                        t
                        for t in cache_translations[Path(modelFile).parent.name]["trs"][
                            "google"
                        ]
                        if t
                    ]
                    if Path(modelFile).parent.name in cache_translations
                    and (
                        "google"
                        in cache_translations[Path(modelFile).parent.name]["trs"]
                    )
                    and (
                        cache_translations[Path(modelFile).parent.name]["trs"]["google"]
                    )
                    else []
                ),
            }

            def add_element_table(include_preview_resolution):
                element = ""
                element += "<tr>"
                if include_preview_resolution:
                    element += f'<td class="col_Preview">{row["Preview"][include_preview_resolution]}</td>'
                element += f'<td class="col_Name">{row["Name"]}</td>'
                element += f'<td class="col_Directory"><a  href="{dirlink}">{row["Directory"]}</a></td>'
                element += f'<td class="col_Location"><a  href="{row["Location"]}">{row["Location"]}</a></td>'
                element += f'<td class="col_File">{row["File"]}</td>'

                element += f'<td class="col_MB">{row["MB"]}</td>'

                element += (
                    f'<td class="col_Google_Tr_Dirname">{row["Google_Tr_Dirname"]}</td>'
                )
                element += (
                    f'<td class="col_Google_Tr_Name">{row["Google_Tr_Name"]}</td>'
                )
                element += (
                    f'<td class="col_Official_Dirname">{row["Official_Dirname"]}</td>'
                )
                element += f'<td class="col_Official_Name">{row["Official_Name"]}</td>'
                element += "</tr>\n"
                return element

            html_page_text = add_element_table(False)
            f_text.write(html_page_text)
            for ir in img_resolution_output:
                f_table[ir].write(add_element_table(ir))

            def add_element_catalogue(include_preview_resolution):
                html_page_catalogue = ""
                html_page_catalogue += '<figure  class="image_cell">'
                html_page_catalogue += row["Preview"][include_preview_resolution]
                html_page_catalogue += f"<figcaption>"
                html_page_catalogue += f'{row["Directory"]}'
                html_page_catalogue += f'  \t({row["MB"]}MB)'
                html_page_catalogue += (
                    f'\t<a href="{row["Location"]}" class="catalogue_location">'
                )
                html_page_catalogue += "  <div>"
                html_page_catalogue += f'\t{row["Location"]}'
                html_page_catalogue += "</div>"
                html_page_catalogue += f"</a>"
                html_page_catalogue += "  <div>"
                html_page_catalogue += (
                    f'\t{"<br>".join(row["Google_Tr_Dirname"].split(", "))}'
                )
                html_page_catalogue += "</div>"
                html_page_catalogue += "  <div>"
                html_page_catalogue += f'\t{"<br>".join(row["Google_Tr_Name"].split(", "))}'
                html_page_catalogue += "</div>"
                html_page_catalogue += "  <div>"
                html_page_catalogue += f'\t{row["Official_Dirname"]}'
                html_page_catalogue += "</div>"
                html_page_catalogue += "  <div>"
                html_page_catalogue += f'\t{row["Official_Name"]}'
                html_page_catalogue += "</div>"
                html_page_catalogue += f"</figcaption>"
                html_page_catalogue += "</figure>\n"
                return html_page_catalogue

            for ir in img_resolution_output:
                f_catalogue[ir].write(add_element_catalogue(ir))

        def end_html():
            html_page_table = ""
            html_page_table += "</table>"
            html_page_table += "</body>"
            html_page_table += "</html>"
            return html_page_table

        eh = end_html()
        f_text.write(eh)
        for ir in img_resolution_output:
            f_table[ir].write(eh)
            html_page_catalogue = ""
            html_page_catalogue += "</body>"
            html_page_catalogue += "</html>"
            f_catalogue[ir].write(html_page_catalogue)


        for ir in img_resolution_output:
            f_table[ir].close()
            f_catalogue[ir].close()

        print("--- Elapsed %s seconds ---" % (time.time() - start_time))


if __name__ == "__main__":
    main()
