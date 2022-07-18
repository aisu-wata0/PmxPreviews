
import sys
from pathlib import Path
import win32com.client
import logging
import re

logging.basicConfig(level=logging.WARN)
# %%
# %%
def main():
	if len(sys.argv) != 2:
		print("Usage: python translation_cache_generator.py [path_to_directory_yourd_like_the_tree_translated]")
		return
		
	directoryPath = sys.argv[1]

	links = []
	links = Path(directoryPath).glob("**/*.lnk")
	for l in links:
		shell = win32com.client.Dispatch("WScript.Shell")
		shortcut = shell.CreateShortCut(str(l))

		link_new_path = l.parent / Path(shortcut.Targetpath).name
		link_target = ""
		link_target_up = ""
		starting_point = l.parent

		logging.info("+++++++")
		logging.info("str(l)", str(l))
		logging.info("Path(shortcut.Targetpath)", Path(shortcut.Targetpath))
		while not link_target and len(str(starting_point)) > 4:
			try:
				logging.info("starting_point", starting_point)
				link_target = str(Path(shortcut.Targetpath).relative_to(starting_point))
			except ValueError:
				starting_point = starting_point.parent
				link_target_up += '..\\'
		link_target = link_target_up + link_target

		logging.info("starting_point", starting_point)

		if len(re.sub(r"[.\\]", "", link_target)) > 1 and len(str(starting_point)) > 3:
			print(fr'mklink /D "{link_new_path}"        "{link_target}"')
			print(f'echo rm "{str(l)}"')
		else:
			print(f'echo # Failed link: "{str(l)}"         with target: "{shortcut.Targetpath}"')

if __name__ == "__main__":
	main()