; ModuleID = 'minigo_module'
source_filename = "minigo_module"

@fmt = private unnamed_addr constant [3 x i8] c"%d\00", align 1
@newline = private unnamed_addr constant [2 x i8] c"\0A\00", align 1

declare i32 @printf(ptr, ...)

declare i32 @fflush(ptr)

define void @main() {
entry:
  %x = alloca i32, align 4
  store i32 42, ptr %x, align 4
  %x1 = load i32, ptr %x, align 4
  %0 = call i32 (ptr, ...) @printf(ptr @fmt, i32 %x1)
  %1 = call i32 (ptr, ...) @printf(ptr @newline)
  %2 = call i32 @fflush(ptr null)
  ret void
}
