; ModuleID = 'minigo_module'
source_filename = "minigo_module"

@x = global i32 42
@fmt = private unnamed_addr constant [3 x i8] c"%d\00", align 1
@newline = private unnamed_addr constant [2 x i8] c"\0A\00", align 1
@fmt.1 = private unnamed_addr constant [3 x i8] c"%d\00", align 1
@newline.2 = private unnamed_addr constant [2 x i8] c"\0A\00", align 1
@fmt.3 = private unnamed_addr constant [3 x i8] c"%d\00", align 1
@newline.4 = private unnamed_addr constant [2 x i8] c"\0A\00", align 1

declare i32 @printf(ptr, ...)

declare i32 @fflush(ptr)

define void @main() {
entry:
  %x = load i32, ptr @x, align 4
  %0 = call i32 (ptr, ...) @printf(ptr @fmt, i32 %x)
  %1 = call i32 (ptr, ...) @printf(ptr @newline)
  %2 = call i32 @fflush(ptr null)
  %3 = call i32 (ptr, ...) @printf(ptr @fmt.1, i32 10)
  %4 = call i32 (ptr, ...) @printf(ptr @newline.2)
  %5 = call i32 @fflush(ptr null)
  %6 = call i32 (ptr, ...) @printf(ptr @fmt.3, i32 20)
  %7 = call i32 (ptr, ...) @printf(ptr @newline.4)
  %8 = call i32 @fflush(ptr null)
  ret void
}
