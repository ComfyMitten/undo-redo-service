@tool
extends EditorPlugin

const AUTOLOAD_NAME = "UndoRedoService"


func _enable_plugin() -> void:
	add_autoload_singleton(AUTOLOAD_NAME, "res://addons/undo_redo_service/undo_redo_service.gd")


func _disable_plugin() -> void:
	remove_autoload_singleton(AUTOLOAD_NAME)
